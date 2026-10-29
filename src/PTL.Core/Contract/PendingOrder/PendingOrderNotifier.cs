using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Notifications;

namespace PTL.Core.Contract.PendingOrder;

// Online contract order emails - legacy email.vb ProcessOnlineContractOrderEmails. The rows of tblOnlineContractOrderEmail
// queued by a decision are the record of what still has to go out: each is sent through GOV.UK Notify and marked sent only
// afterwards, so a failed send stays unsent. There is no retry.
public interface IPendingOrderNotifier
{
    // Sends one decided order's emails straight after the decision was saved. Never throws: whatever fails stays unsent.
    Task SendForOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default);
}

public sealed partial class PendingOrderNotifier(
    IPendingOrderRepository pendingOrderRepository,
    IPendingOrderEmailPersonaliser personaliser,
    INotifyClient notifyClient,
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

    private async Task SendAsync(Guid pendingContractId, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var unsent = (await pendingOrderRepository.GetUnsentNotificationsAsync(cancellationToken))
            .Where(e => e.PendingContractId == pendingContractId)
            .ToList();
        if (unsent.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var outcomes = new List<EmailSendOutcome>(unsent.Count);
        foreach (var email in unsent)
        {
            outcomes.Add(await SendOneAsync(email, now, cancellationToken));
        }

        var sent = outcomes.Count(o => o == EmailSendOutcome.Sent);
        var skipped = outcomes.Count(o => o == EmailSendOutcome.Skipped);
        var failed = outcomes.Count(o => o == EmailSendOutcome.Failed);
        LogRunSummary(logger, unsent.Count, sent, skipped, failed);
    }

    private async Task<EmailSendOutcome> SendOneAsync(OnlineOrderEmailEntity email, DateTime now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email.Email))
        {
            LogSkipped(logger, email.Id, email.PendingContractId, "no email address");
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
            LogSendFailed(logger, email.Id, email.PendingContractId, status, templateId, ex);
            return EmailSendOutcome.Failed;
        }
    }

    [LoggerMessage(EventId = 61, Level = LogLevel.Information,
        Message = "Online contract order email sent. NotificationId={NotificationId} PendingContractId={PendingContractId} Status={Status} TemplateId={TemplateId}")]
    private static partial void LogSent(ILogger logger, Guid notificationId, Guid pendingContractId, string status, string templateId);

    [LoggerMessage(EventId = 62, Level = LogLevel.Error,
        Message = "Online contract order email could not be sent; it stays unsent. NotificationId={NotificationId} PendingContractId={PendingContractId} Status={Status} TemplateId={TemplateId}")]
    private static partial void LogSendFailed(ILogger logger, Guid notificationId, Guid pendingContractId, string status, string templateId, Exception exception);

    [LoggerMessage(EventId = 63, Level = LogLevel.Warning,
        Message = "Online contract order email stays unsent: {Reason}. NotificationId={NotificationId} PendingContractId={PendingContractId}")]
    private static partial void LogSkipped(ILogger logger, Guid notificationId, Guid pendingContractId, string reason);

    [LoggerMessage(EventId = 64, Level = LogLevel.Information,
        Message = "Online contract order email run complete. Unsent={Unsent} Sent={Sent} Skipped={Skipped} Failed={Failed}")]
    private static partial void LogRunSummary(ILogger logger, int unsent, int sent, int skipped, int failed);

    [LoggerMessage(EventId = 65, Level = LogLevel.Error,
        Message = "Online contract order emails could not be sent after the decision was saved; the unsent ones stay unsent. PendingContractId={PendingContractId}")]
    private static partial void LogImmediateSendFailed(ILogger logger, Guid pendingContractId, Exception exception);
}
