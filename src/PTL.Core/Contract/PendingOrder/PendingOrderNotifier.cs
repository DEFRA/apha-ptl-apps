using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Lookup;
using PTL.Core.Notifications;
using PTL.Core.Participant;
using PTL.Core.Scheme;
using ICustomerRepository = PTL.Core.Customer.ICustomerRepository;

namespace PTL.Core.Contract.PendingOrder;

// Online contract order emails - legacy email.vb ProcessOnlineContractOrderEmails. The unsent rows of
// tblOnlineContractOrderEmail are the record of what still has to go out: each is sent through GOV.UK Notify and
// marked sent only afterwards, so a failed send stays unsent for the next scheduled run.
public interface IPendingOrderNotifier
{
    // Sends one decided order's emails straight after the decision was saved. Never throws: whatever fails stays unsent for the scheduler.
    Task SendForOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default);

    // The scheduled retry - every unsent row.
    Task<OnlineOrderEmailRunSummary> SendUnsentAsync(CancellationToken cancellationToken = default);
}

public sealed record OnlineOrderEmailRunSummary(int Unsent, int Sent, int Skipped, int Failed)
{
    public static readonly OnlineOrderEmailRunSummary Empty = new(0, 0, 0, 0);
}

public sealed partial class PendingOrderNotifier(
    IPendingOrderRepository pendingOrderRepository,
    IContractRepository contractRepository,
    ISchemeRepository schemeRepository,
    ICustomerRepository customerRepository,
    IParticipantRepository participantRepository,
    ILookupRepository lookupRepository,
    INotifyClient notifyClient,
    PendingOrderEmailGuard guard,
    IOptions<OnlineOrderEmailOptions> options,
    TimeProvider timeProvider,
    ILogger<PendingOrderNotifier> logger) : IPendingOrderNotifier
{
    // Legacy getSchemeMonths lists the contract year from April; index 0 of ContractItemLine.Months is January.
    private static readonly int[] FinancialYearOrder = [3, 4, 5, 6, 7, 8, 9, 10, 11, 0, 1, 2];
    private static readonly string[] MonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    public async Task SendForOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendAsync(pendingContractId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogImmediateSendFailed(logger, pendingContractId, ex);
        }
    }

    public Task<OnlineOrderEmailRunSummary> SendUnsentAsync(CancellationToken cancellationToken = default) =>
        SendAsync(null, cancellationToken);

    private async Task<OnlineOrderEmailRunSummary> SendAsync(Guid? pendingContractId, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return OnlineOrderEmailRunSummary.Empty;
        }

        await guard.Lock.WaitAsync(cancellationToken);
        try
        {
            var unsent = (await pendingOrderRepository.GetUnsentNotificationsAsync(cancellationToken))
                .Where(e => pendingContractId is null || e.PendingContractId == pendingContractId)
                .ToList();
            if (unsent.Count == 0)
            {
                return OnlineOrderEmailRunSummary.Empty;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            int sent = 0, skipped = 0, failed = 0;

            foreach (var email in unsent)
            {
                var templateId = email.IsApproved ? settings.ApprovedTemplateId : settings.DeclinedTemplateId;
                if (string.IsNullOrWhiteSpace(email.Email) || string.IsNullOrWhiteSpace(templateId))
                {
                    skipped++;
                    if (guard.FirstWarningToday(email.Id, now.Date))
                    {
                        LogSkipped(logger, email.Id, email.PendingContractId, string.IsNullOrWhiteSpace(email.Email) ? "no email address" : "no Notify template id configured");
                    }

                    continue;
                }

                try
                {
                    var personalisation = await PersonaliseAsync(email, cancellationToken);
                    await notifyClient.SendEmailAsync(templateId, email.Email, personalisation, email.Id.ToString(), cancellationToken);
                    await pendingOrderRepository.MarkNotificationSentAsync(email.Id, now, CancellationToken.None);
                    sent++;
                    LogSent(logger, email.Id, email.PendingContractId, email.IsApproved ? "Approved" : "Declined", templateId);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    if (guard.FirstWarningToday(email.Id, now.Date))
                    {
                        LogSendFailed(logger, email.Id, email.PendingContractId, email.IsApproved ? "Approved" : "Declined", templateId, ex);
                    }
                }
            }

            var summary = new OnlineOrderEmailRunSummary(unsent.Count, sent, skipped, failed);
            LogRunSummary(logger, summary.Unsent, summary.Sent, summary.Skipped, summary.Failed);
            return summary;
        }
        finally
        {
            guard.Lock.Release();
        }
    }

    // The tokens of the legacy templates: <<ParticipantName>>, <<ApprovedSchemeOrders>>, <<Notes>> and the subject's
    // <<ContractYear>> / <<CustomerName>> (which legacy filled with the QAL number).
    private async Task<Dictionary<string, string>> PersonaliseAsync(OnlineOrderEmailEntity email, CancellationToken cancellationToken)
    {
        // A decided order stays in tblPendingContract (soft-deleted), so this also serves the declined path, where no contract exists.
        var order = await pendingOrderRepository.GetByIdAsync(email.PendingContractId, cancellationToken);
        var customer = order is null ? null : await customerRepository.GetByIdAsync(order.CustomerId, cancellationToken);
        var years = await lookupRepository.GetAllYearsAsync(cancellationToken);

        var personalisation = new Dictionary<string, string>
        {
            ["ParticipantName"] = order is null ? string.Empty : await FirstActiveLabCodeAsync(order.CustomerId, cancellationToken),
            ["CustomerName"] = customer?.QalNumber ?? string.Empty,
            ["status"] = email.IsApproved ? "Approved" : "Declined",
            ["ContractYear"] = years.FirstOrDefault(y => y.YearId == email.YearId)?.Year ?? "####",
            ["Notes"] = email.Notes,
        };

        if (email.IsApproved)
        {
            personalisation["ApprovedSchemeOrders"] = await ApprovedSchemeOrdersAsync(email.ContractId, cancellationToken);
        }

        return personalisation;
    }

    // Legacy intended the first active participant's lab code for both outcomes; its declined path never found one
    // (the pending schemes it searched had already been deleted).
    private async Task<string> FirstActiveLabCodeAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var participants = await participantRepository.GetSummariesAsync(customerId, false, cancellationToken);
        return participants.Count > 0 ? participants[0].LabCode : string.Empty;
    }

    // The legacy HTML table (PT Schemes / Month / Week) as the bullet list GOV.UK Notify can render.
    private async Task<string> ApprovedSchemeOrdersAsync(Guid contractId, CancellationToken cancellationToken)
    {
        var items = await contractRepository.GetContractItemsAsync(contractId, cancellationToken);
        if (items is null)
        {
            return string.Empty;
        }

        var lines = new List<string>(items.Schemes.Count);
        foreach (var group in items.Schemes)
        {
            var scheme = await schemeRepository.GetByIdAsync(group.SchemeId, cancellationToken);
            var months = FinancialYearOrder.Where(m => group.Participants.Any(p => p.Months[m])).Select(m => MonthNames[m]);
            var week = (scheme?.WeekNumber ?? 0).ToString(CultureInfo.InvariantCulture);
            lines.Add($"* {group.SchemeIdentifier}:{group.SchemeName} - Month: {string.Join(' ', months)} - Week: {week}");
        }

        return string.Join('\n', lines);
    }

    [LoggerMessage(EventId = 61, Level = LogLevel.Information,
        Message = "Online contract order email sent. NotificationId={NotificationId} PendingContractId={PendingContractId} Status={Status} TemplateId={TemplateId}")]
    private static partial void LogSent(ILogger logger, Guid notificationId, Guid pendingContractId, string status, string templateId);

    [LoggerMessage(EventId = 62, Level = LogLevel.Error,
        Message = "Online contract order email could not be sent; it stays unsent and the scheduler will retry. NotificationId={NotificationId} PendingContractId={PendingContractId} Status={Status} TemplateId={TemplateId}")]
    private static partial void LogSendFailed(ILogger logger, Guid notificationId, Guid pendingContractId, string status, string templateId, Exception exception);

    [LoggerMessage(EventId = 63, Level = LogLevel.Warning,
        Message = "Online contract order email stays unsent: {Reason}. NotificationId={NotificationId} PendingContractId={PendingContractId}")]
    private static partial void LogSkipped(ILogger logger, Guid notificationId, Guid pendingContractId, string reason);

    [LoggerMessage(EventId = 64, Level = LogLevel.Information,
        Message = "Online contract order email run complete. Unsent={Unsent} Sent={Sent} Skipped={Skipped} Failed={Failed}")]
    private static partial void LogRunSummary(ILogger logger, int unsent, int sent, int skipped, int failed);

    [LoggerMessage(EventId = 65, Level = LogLevel.Error,
        Message = "Online contract order emails could not be sent after the decision was saved; the scheduler will retry. PendingContractId={PendingContractId}")]
    private static partial void LogImmediateSendFailed(ILogger logger, Guid pendingContractId, Exception exception);
}
