using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Notifications;

namespace PTL.Core.Invoice;

public sealed class InvoiceService(
    IInvoiceRepository repository,
    IInvoiceStorageService storage,
    INotifyClient notifyClient,
    IOptions<InvoiceStorageOptions> storageOptions,
    IOptions<InvoiceNotificationOptions> notificationOptions,
    ILogger<InvoiceService> logger) : IInvoiceService
{
    private static readonly Action<ILogger, int, string, Exception?> LogGeneratedMessage =
        LoggerMessage.Define<int, string>(
            LogLevel.Information,
            new EventId(1, nameof(LogGeneratedMessage)),
            "Generated invoices for {ContractCount} contracts, stored at {StorageKey}");

    private static readonly Action<ILogger, string, Exception> LogGenerationFailedMessage =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2, nameof(LogGenerationFailedMessage)),
            "Invoice generation failed at stage {Stage}");

    private static readonly Action<ILogger, string, Exception?> LogNotifySendingMessage =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(4, nameof(LogNotifySendingMessage)),
            "Sending GOV.UK Notify email: {NotifyCallDetails}");

    // TEMPORARY DIAGNOSTIC - remove once the "Invoices could not be generated" root cause
    // investigation is closed. Logs state before each pipeline step so the hidden exception's
    // stage is unambiguous without changing the legacy-matching message returned to the caller.
    private static readonly Action<ILogger, string, Exception?> LogDiagnosticMessage =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(5, nameof(LogDiagnosticMessage)),
            "[TEMP DIAGNOSTIC] {Detail}");

    private static readonly Action<ILogger, string, Exception> LogNotificationFailedMessage =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, nameof(LogNotificationFailedMessage)),
            "Failed to notify {Recipient} that the invoice CSV is ready");

    public async Task<PendingInvoiceSummary> GetPendingSummaryAsync(CancellationToken cancellationToken = default)
    {
        var data = await repository.GetPendingInvoiceDataAsync(cancellationToken);

        // Informational counts only - Opt Out / Non Fee Paying never exclude a contract/item from
        // generation (approved business decision, docs/migration/invoice-migration.md).
        return new PendingInvoiceSummary(
            EligibleContractCount: data.Contracts.Count,
            OptOutContractCount: data.Contracts.Count(c => c.OptOutOfInvoiceGeneration),
            NonFeePayingItemCount: data.Items.Count(i => i.NonFeePaying));
    }

    public async Task<InvoiceGenerationOutcome> GenerateInvoicesAsync(string generatedBy, CancellationToken cancellationToken = default)
    {
        // Tracks which stage was in flight when an exception is thrown below, so structured logs
        // pinpoint the real failure point (contract retrieval / CSV+S3 / mark-invoiced+audit /
        // notify) without changing the legacy-matching message returned to the caller.
        var stage = "ContractRetrieval";
        try
        {
            var data = await repository.GetPendingInvoiceDataAsync(cancellationToken);

            var financialYears = string.Join(",", data.Contracts.Select(c => c.YearId).Distinct());
            LogDiagnosticMessage(logger, $"Stage={stage} FinancialYear(s)=[{financialYears}] ContractCount={data.Contracts.Count}", null);

            // Captured once so the storage key's year partition and the notification's
            // generationDateTime personalisation refer to the exact same instant.
            var generatedAt = DateTime.UtcNow;

            stage = "CsvGenerationAndS3Upload";
            var csv = InvoiceCsvBuilder.Build(data);
            var csvBytes = Encoding.UTF8.GetBytes(csv);
            var storageKey = BuildStorageKey(generatedAt);
            LogDiagnosticMessage(logger, $"Stage={stage} GeneratedCsvSizeBytes={csvBytes.Length} S3ObjectKey={storageKey}", null);
            await storage.SaveAsync(storageKey, csvBytes, "text/csv", cancellationToken);

            // Mirrors legacy's single TransactionScope around sppUpdateInvoiceItems +
            // spiAuditInvoiceGeneration - every eligible contract is marked invoiced regardless of
            // whether it produced any CSV row (a contract with zero contract items is still
            // eligible by the SQL-level criteria and is still marked invoiced, exactly as legacy's
            // SQL-only sppUpdateInvoiceItems does).
            stage = "MarkInvoicedAndAudit";
            LogDiagnosticMessage(logger, $"Stage={stage} ContractCount={data.Contracts.Count} AuditWho={generatedBy}", null);
            await repository.MarkInvoicedAndRecordAuditAsync(generatedBy, cancellationToken);

            LogGeneratedMessage(logger, data.Contracts.Count, storageKey, null);

            stage = "Notify";
            await NotifyRecipientsAsync(stage, storageKey, csvBytes, generatedAt, cancellationToken);

            return new InvoiceGenerationOutcome(Success: true, ContractCount: data.Contracts.Count, CsvStorageKey: storageKey, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            LogGenerationFailedMessage(logger, stage, ex);
            return new InvoiceGenerationOutcome(Success: false, ContractCount: 0, CsvStorageKey: null, ErrorMessage: "Invoices could not be generated. Please try again later.");
        }
    }

    public Task ResetInvoicedFlagsAsync(CancellationToken cancellationToken = default) =>
        repository.ResetInvoicedFlagsAsync(cancellationToken);

    public Task<IReadOnlyList<InvoiceAuditEntity>> GetAuditHistoryAsync(CancellationToken cancellationToken = default) =>
        repository.GetAuditHistoryAsync(cancellationToken);

    private async Task NotifyRecipientsAsync(string stage, string storageKey, byte[] csvBytes, DateTime generatedAt, CancellationToken cancellationToken)
    {
        var options = notificationOptions.Value;
        LogDiagnosticMessage(logger, $"Stage={stage} TemplateId={options.TemplateId} RecipientCount={options.Recipients.Count}", null);
        if (string.IsNullOrWhiteSpace(options.TemplateId) || options.Recipients.Count == 0)
        {
            return;
        }

        if (csvBytes.Length > NotifyFileAttachment.MaxFileSizeBytes)
        {
            LogDiagnosticMessage(logger, $"Stage={stage} WARNING CsvSizeBytes={csvBytes.Length} exceeds GOV.UK Notify's {NotifyFileAttachment.MaxFileSizeBytes}-byte file limit - send will likely be rejected by Notify", null);
        }

        // Legacy subject/body: "FAO IT Unit Weybridge - Proficiency Testing Invoice File" /
        // "The attached invoices were generated from the Proficiency Testing system on
        // {generationDateTime}" (DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")) - the Notify portal
        // template referenced by TemplateId supplies this wording plus the file-download placeholder.
        var personalisation = new Dictionary<string, string>
        {
            ["csv_reference"] = storageKey,
            ["generationDateTime"] = generatedAt.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture)
        };

        // Legacy's System.Net.Mail.Attachment(path) names the attachment after the file itself, not
        // its full archive path - the S3 key's last segment reproduces that exactly.
        var filename = storageKey[(storageKey.LastIndexOf('/') + 1)..];

        foreach (var recipient in options.Recipients)
        {
            try
            {
                LogNotifySendingMessage(
                    logger,
                    $"recipient={recipient} templateId={options.TemplateId} filename={filename} personalisation=[{string.Join(", ", personalisation.Select(p => $"{p.Key}={p.Value}"))}]",
                    null);
                await notifyClient.SendEmailWithFileAsync(
                    options.TemplateId,
                    recipient,
                    options.FilePersonalisationKey,
                    csvBytes,
                    filename,
                    personalisation,
                    reference: storageKey,
                    cancellationToken);
                // NotifyClient.SendEmailWithFileAsync does not expose the HTTP response body/status -
                // only whether it threw is observable here, so "response" is logged as Accepted/Failed.
                LogDiagnosticMessage(logger, $"Stage={stage} NotifyResponse=Accepted Recipient={recipient}", null);
            }
            catch (Exception ex)
            {
                // A notification failure does not roll back generation - the invoices are already
                // marked sent and audited; this only means staff must be told some other way.
                LogNotificationFailedMessage(logger, recipient, ex);
                LogDiagnosticMessage(logger, $"Stage={stage} NotifyResponse=Failed Recipient={recipient} Exception={ex.GetType().Name}", null);
            }
        }
    }

    private string BuildStorageKey(DateTime generatedAt)
    {
        var prefix = string.IsNullOrWhiteSpace(storageOptions.Value.Prefix) ? string.Empty : storageOptions.Value.Prefix.Trim('/') + "/";
        var timestamp = generatedAt.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture);
        return $"{prefix}{generatedAt:yyyy}/PT_Invoices_{timestamp}.csv";
    }
}
