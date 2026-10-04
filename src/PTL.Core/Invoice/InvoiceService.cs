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

    private static readonly Action<ILogger, Exception> LogGenerationFailedMessage =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, nameof(LogGenerationFailedMessage)),
            "Invoice generation failed");

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
        try
        {
            var data = await repository.GetPendingInvoiceDataAsync(cancellationToken);

            var csv = InvoiceCsvBuilder.Build(data);
            var storageKey = BuildStorageKey();
            await storage.SaveAsync(storageKey, Encoding.UTF8.GetBytes(csv), "text/csv", cancellationToken);

            // Mirrors legacy's single TransactionScope around sppUpdateInvoiceItems +
            // spiAuditInvoiceGeneration - every eligible contract is marked invoiced regardless of
            // whether it produced any CSV row (a contract with zero contract items is still
            // eligible by the SQL-level criteria and is still marked invoiced, exactly as legacy's
            // SQL-only sppUpdateInvoiceItems does).
            await repository.MarkInvoicedAndRecordAuditAsync(generatedBy, cancellationToken);

            LogGeneratedMessage(logger, data.Contracts.Count, storageKey, null);

            await NotifyRecipientsAsync(storageKey, cancellationToken);

            return new InvoiceGenerationOutcome(Success: true, ContractCount: data.Contracts.Count, CsvStorageKey: storageKey, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            LogGenerationFailedMessage(logger, ex);
            return new InvoiceGenerationOutcome(Success: false, ContractCount: 0, CsvStorageKey: null, ErrorMessage: "Invoices could not be generated. Please try again later.");
        }
    }

    public Task ResetInvoicedFlagsAsync(CancellationToken cancellationToken = default) =>
        repository.ResetInvoicedFlagsAsync(cancellationToken);

    public Task<IReadOnlyList<InvoiceAuditEntity>> GetAuditHistoryAsync(CancellationToken cancellationToken = default) =>
        repository.GetAuditHistoryAsync(cancellationToken);

    private async Task NotifyRecipientsAsync(string storageKey, CancellationToken cancellationToken)
    {
        var options = notificationOptions.Value;
        if (string.IsNullOrWhiteSpace(options.TemplateId) || options.Recipients.Count == 0)
        {
            return;
        }

        var personalisation = new Dictionary<string, string> { ["csv_reference"] = storageKey };

        foreach (var recipient in options.Recipients)
        {
            try
            {
                await notifyClient.SendEmailAsync(options.TemplateId, recipient, personalisation, reference: storageKey, cancellationToken);
            }
            catch (Exception ex)
            {
                // A notification failure does not roll back generation - the invoices are already
                // marked sent and audited; this only means staff must be told some other way.
                LogNotificationFailedMessage(logger, recipient, ex);
            }
        }
    }

    private string BuildStorageKey()
    {
        var prefix = string.IsNullOrWhiteSpace(storageOptions.Value.Prefix) ? string.Empty : storageOptions.Value.Prefix.Trim('/') + "/";
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture);
        return $"{prefix}PT_Invoices_{timestamp}.csv";
    }
}
