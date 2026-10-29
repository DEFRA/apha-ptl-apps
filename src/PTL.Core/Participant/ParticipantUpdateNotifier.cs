using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Notifications;

namespace PTL.Core.Participant;

// Participant details update emails - legacy email.vb ProcessUpdateParticipantDetailsEmails. The rows of
// tblUpdatedParticipantDetailsEmail queued by a decision are the record of what still has to go out: each is sent through
// GOV.UK Notify and marked sent only afterwards, so a failed send stays unsent. There is no retry.
public interface IParticipantUpdateNotifier
{
    // Sends one decided update's emails straight after the decision was saved. Never throws: whatever fails stays unsent.
    Task SendForUpdateAsync(Guid pendingParticipantUpdateId, CancellationToken cancellationToken = default);
}

public sealed partial class ParticipantUpdateNotifier(
    IPendingParticipantUpdateRepository repository,
    INotifyClient notifyClient,
    IOptions<ParticipantUpdateEmailOptions> options,
    TimeProvider timeProvider,
    ILogger<ParticipantUpdateNotifier> logger) : IParticipantUpdateNotifier
{
    public async Task SendForUpdateAsync(Guid pendingParticipantUpdateId, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendAsync(pendingParticipantUpdateId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogImmediateSendFailed(logger, pendingParticipantUpdateId, ex);
        }
    }

    private async Task SendAsync(Guid pendingParticipantUpdateId, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var unsent = (await repository.GetUnsentNotificationsAsync(cancellationToken))
            .Where(e => e.PendingParticipantUpdateId == pendingParticipantUpdateId)
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

    private async Task<EmailSendOutcome> SendOneAsync(ParticipantUpdateEmailEntity email, DateTime now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email.Email))
        {
            LogSkipped(logger, email.Id, email.PendingParticipantUpdateId, "no email address");
            return EmailSendOutcome.Skipped;
        }

        var templateId = email.IsApproved ? ParticipantUpdateEmailTemplates.AcceptedId : ParticipantUpdateEmailTemplates.DeclinedId;
        var status = email.IsApproved ? "Accepted" : "Declined";
        try
        {
            await notifyClient.SendEmailAsync(templateId, email.Email, Personalise(email, status), email.Id.ToString(), cancellationToken);
            await repository.MarkNotificationSentAsync(email.Id, now, CancellationToken.None);
            LogSent(logger, email.Id, email.PendingParticipantUpdateId, status, templateId);
            return EmailSendOutcome.Sent;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSendFailed(logger, email.Id, email.PendingParticipantUpdateId, status, templateId, ex);
            return EmailSendOutcome.Failed;
        }
    }

    // The tokens of the legacy templates: <<ParticipantName>> (the lab code), <<Notes>> and the subject's <<Customer Name>> (the
    // QAL number) and <<status>>.
    private static Dictionary<string, string> Personalise(ParticipantUpdateEmailEntity email, string status) =>
        new()
        {
            ["ParticipantName"] = email.LabCode,
            ["CustomerName"] = email.QalNumber ?? string.Empty,
            ["status"] = status,
            ["Notes"] = email.Notes,
        };

    [LoggerMessage(EventId = 91, Level = LogLevel.Information,
        Message = "Participant update email sent. NotificationId={NotificationId} PendingParticipantUpdateId={PendingParticipantUpdateId} Status={Status} TemplateId={TemplateId}")]
    private static partial void LogSent(ILogger logger, Guid notificationId, Guid pendingParticipantUpdateId, string status, string templateId);

    [LoggerMessage(EventId = 92, Level = LogLevel.Error,
        Message = "Participant update email could not be sent; it stays unsent. NotificationId={NotificationId} PendingParticipantUpdateId={PendingParticipantUpdateId} Status={Status} TemplateId={TemplateId}")]
    private static partial void LogSendFailed(ILogger logger, Guid notificationId, Guid pendingParticipantUpdateId, string status, string templateId, Exception exception);

    [LoggerMessage(EventId = 93, Level = LogLevel.Warning,
        Message = "Participant update email stays unsent: {Reason}. NotificationId={NotificationId} PendingParticipantUpdateId={PendingParticipantUpdateId}")]
    private static partial void LogSkipped(ILogger logger, Guid notificationId, Guid pendingParticipantUpdateId, string reason);

    [LoggerMessage(EventId = 94, Level = LogLevel.Information,
        Message = "Participant update email run complete. Unsent={Unsent} Sent={Sent} Skipped={Skipped} Failed={Failed}")]
    private static partial void LogRunSummary(ILogger logger, int unsent, int sent, int skipped, int failed);

    [LoggerMessage(EventId = 95, Level = LogLevel.Error,
        Message = "Participant update emails could not be sent after the decision was saved; the unsent ones stay unsent. PendingParticipantUpdateId={PendingParticipantUpdateId}")]
    private static partial void LogImmediateSendFailed(ILogger logger, Guid pendingParticipantUpdateId, Exception exception);
}
