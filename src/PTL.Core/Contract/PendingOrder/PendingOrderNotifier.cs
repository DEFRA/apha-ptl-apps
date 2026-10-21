using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Notifications;

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
    IPendingOrderEmailPersonaliser personaliser,
    INotifyClient notifyClient,
    EmailSendGuard guard,
    IOptions<OnlineOrderEmailOptions> options,
    TimeProvider timeProvider,
    ILogger<PendingOrderNotifier> logger) : IPendingOrderNotifier
{
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
            var outcomes = new List<EmailSendOutcome>(unsent.Count);
            foreach (var email in unsent)
            {
                outcomes.Add(await SendOneAsync(email, now, cancellationToken));
            }

            var summary = new OnlineOrderEmailRunSummary(
                unsent.Count,
                outcomes.Count(o => o == EmailSendOutcome.Sent),
                outcomes.Count(o => o == EmailSendOutcome.Skipped),
                outcomes.Count(o => o == EmailSendOutcome.Failed));
            LogRunSummary(logger, summary.Unsent, summary.Sent, summary.Skipped, summary.Failed);
            return summary;
        }
        finally
        {
            guard.Lock.Release();
        }
    }

    private async Task<EmailSendOutcome> SendOneAsync(OnlineOrderEmailEntity email, DateTime now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email.Email))
        {
            if (guard.FirstWarningToday(email.Id, now.Date))
            {
                LogSkipped(logger, email.Id, email.PendingContractId, "no email address");
            }

            return EmailSendOutcome.Skipped;
        }

        var templateId = email.IsApproved ? OnlineOrderEmailTemplates.ApprovedId : OnlineOrderEmailTemplates.DeclinedId;
        var status = email.IsApproved ? "Approved" : "Declined";
        try
        {
            var personalisation = await personaliser.PersonaliseAsync(email, cancellationToken);
            await notifyClient.SendEmailAsync(templateId, email.Email, personalisation, email.Id.ToString(), cancellationToken);
            await pendingOrderRepository.MarkNotificationSentAsync(email.Id, now, CancellationToken.None);
            LogSent(logger, email.Id, email.PendingContractId, status, templateId);
            return EmailSendOutcome.Sent;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (guard.FirstWarningToday(email.Id, now.Date))
            {
                LogSendFailed(logger, email.Id, email.PendingContractId, status, templateId, ex);
            }

            return EmailSendOutcome.Failed;
        }
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
