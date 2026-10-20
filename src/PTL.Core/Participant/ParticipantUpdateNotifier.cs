using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Notifications;

namespace PTL.Core.Participant;

// Participant details update emails - legacy email.vb ProcessUpdateParticipantDetailsEmails. The unsent rows of
// tblUpdatedParticipantDetailsEmail are the record of what still has to go out: each is sent through GOV.UK Notify and
// marked sent only afterwards, so a failed send stays unsent for the next scheduled run.
public interface IParticipantUpdateNotifier
{
    // Sends one decided update's emails straight after the decision was saved. Never throws: whatever fails stays unsent for the scheduler.
    Task SendForUpdateAsync(Guid pendingParticipantUpdateId, CancellationToken cancellationToken = default);

    // The scheduled retry - every unsent row.
    Task SendUnsentAsync(CancellationToken cancellationToken = default);
}

public sealed partial class ParticipantUpdateNotifier(
    IPendingParticipantUpdateRepository repository,
    INotifyClient notifyClient,
    EmailSendGuard guard,
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

    public Task SendUnsentAsync(CancellationToken cancellationToken = default) => SendAsync(null, cancellationToken);

    private async Task SendAsync(Guid? pendingParticipantUpdateId, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        await guard.Lock.WaitAsync(cancellationToken);
        try
        {
            var unsent = (await repository.GetUnsentNotificationsAsync(cancellationToken))
                .Where(e => pendingParticipantUpdateId is null || e.PendingParticipantUpdateId == pendingParticipantUpdateId)
                .ToList();
            if (unsent.Count == 0)
            {
                return;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            int sent = 0, skipped = 0, failed = 0;

            foreach (var email in unsent)
            {
                var templateId = email.IsApproved ? ParticipantUpdateEmailTemplates.AcceptedId : ParticipantUpdateEmailTemplates.DeclinedId;
                if (string.IsNullOrWhiteSpace(email.Email))
                {
                    skipped++;
                    if (guard.FirstWarningToday(email.Id, now.Date))
                    {
                        LogSkipped(logger, email.Id, email.PendingParticipantUpdateId, "no email address");
                    }

                    continue;
                }

                var status = email.IsApproved ? "Accepted" : "Declined";
                try
                {
                    await notifyClient.SendEmailAsync(templateId, email.Email, Personalise(email, status), email.Id.ToString(), cancellationToken);
                    await repository.MarkNotificationSentAsync(email.Id, now, CancellationToken.None);
                    sent++;
                    LogSent(logger, email.Id, email.PendingParticipantUpdateId, status, templateId);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    if (guard.FirstWarningToday(email.Id, now.Date))
                    {
                        LogSendFailed(logger, email.Id, email.PendingParticipantUpdateId, status, templateId, ex);
                    }
                }
            }

            LogRunSummary(logger, unsent.Count, sent, skipped, failed);
        }
        finally
        {
            guard.Lock.Release();
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
        Message = "Participant update email could not be sent; it stays unsent and the scheduler will retry. NotificationId={NotificationId} PendingParticipantUpdateId={PendingParticipantUpdateId} Status={Status} TemplateId={TemplateId}")]
    private static partial void LogSendFailed(ILogger logger, Guid notificationId, Guid pendingParticipantUpdateId, string status, string templateId, Exception exception);

    [LoggerMessage(EventId = 93, Level = LogLevel.Warning,
        Message = "Participant update email stays unsent: {Reason}. NotificationId={NotificationId} PendingParticipantUpdateId={PendingParticipantUpdateId}")]
    private static partial void LogSkipped(ILogger logger, Guid notificationId, Guid pendingParticipantUpdateId, string reason);

    [LoggerMessage(EventId = 94, Level = LogLevel.Information,
        Message = "Participant update email run complete. Unsent={Unsent} Sent={Sent} Skipped={Skipped} Failed={Failed}")]
    private static partial void LogRunSummary(ILogger logger, int unsent, int sent, int skipped, int failed);

    [LoggerMessage(EventId = 95, Level = LogLevel.Error,
        Message = "Participant update emails could not be sent after the decision was saved; the scheduler will retry. PendingParticipantUpdateId={PendingParticipantUpdateId}")]
    private static partial void LogImmediateSendFailed(ILogger logger, Guid pendingParticipantUpdateId, Exception exception);
}
