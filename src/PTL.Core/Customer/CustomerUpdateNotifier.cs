using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Notifications;

namespace PTL.Core.Customer;

// Customer details update emails - legacy email.vb ProcessUpdateCustomerDetailsEmails. The unsent rows of
// tblUpdatedCustomerDetailsEmail are the record of what still has to go out: each is sent through GOV.UK Notify and
// marked sent only afterwards, so a failed send stays unsent for the next scheduled run.
public interface ICustomerUpdateNotifier
{
    // Sends one decided update's emails straight after the decision was saved. Never throws: whatever fails stays unsent for the scheduler.
    Task SendForUpdateAsync(Guid pendingCustomerUpdateId, CancellationToken cancellationToken = default);

    // The scheduled retry - every unsent row.
    Task SendUnsentAsync(CancellationToken cancellationToken = default);
}

public sealed partial class CustomerUpdateNotifier(
    IPendingCustomerUpdateRepository repository,
    INotifyClient notifyClient,
    EmailSendGuard guard,
    IOptions<CustomerUpdateEmailOptions> options,
    TimeProvider timeProvider,
    ILogger<CustomerUpdateNotifier> logger) : ICustomerUpdateNotifier
{
    public async Task SendForUpdateAsync(Guid pendingCustomerUpdateId, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendAsync(pendingCustomerUpdateId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogImmediateSendFailed(logger, pendingCustomerUpdateId, ex);
        }
    }

    public Task SendUnsentAsync(CancellationToken cancellationToken = default) => SendAsync(null, cancellationToken);

    private async Task SendAsync(Guid? pendingCustomerUpdateId, CancellationToken cancellationToken)
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
                .Where(e => pendingCustomerUpdateId is null || e.PendingCustomerUpdateId == pendingCustomerUpdateId)
                .ToList();
            if (unsent.Count == 0)
            {
                return;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            int sent = 0, skipped = 0, failed = 0;

            foreach (var email in unsent)
            {
                var templateId = email.IsApproved ? settings.AcceptedTemplateId : settings.DeclinedTemplateId;
                if (string.IsNullOrWhiteSpace(email.Email) || string.IsNullOrWhiteSpace(templateId))
                {
                    skipped++;
                    if (guard.FirstWarningToday(email.Id, now.Date))
                    {
                        LogSkipped(logger, email.Id, email.PendingCustomerUpdateId, string.IsNullOrWhiteSpace(email.Email) ? "no email address" : "no Notify template id configured");
                    }

                    continue;
                }

                var status = email.IsApproved ? "Accepted" : "Declined";
                try
                {
                    await notifyClient.SendEmailAsync(templateId, email.Email, Personalise(email, status), email.Id.ToString(), cancellationToken);
                    await repository.MarkNotificationSentAsync(email.Id, now, CancellationToken.None);
                    sent++;
                    LogSent(logger, email.Id, email.PendingCustomerUpdateId, status, templateId);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    if (guard.FirstWarningToday(email.Id, now.Date))
                    {
                        LogSendFailed(logger, email.Id, email.PendingCustomerUpdateId, status, templateId, ex);
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

    // The tokens of the legacy templates: <<ParticipantName>> (a lab code), <<Notes>> and the subject's <<Customer Name>> (the
    // QAL number) and <<status>>.
    private static Dictionary<string, string> Personalise(CustomerUpdateEmailEntity email, string status) =>
        new()
        {
            ["ParticipantName"] = email.LabCode ?? string.Empty,
            ["CustomerName"] = email.QalNumber ?? string.Empty,
            ["status"] = status,
            ["Notes"] = email.Notes,
        };

    [LoggerMessage(EventId = 81, Level = LogLevel.Information,
        Message = "Customer update email sent. NotificationId={NotificationId} PendingCustomerUpdateId={PendingCustomerUpdateId} Status={Status} TemplateId={TemplateId}")]
    private static partial void LogSent(ILogger logger, Guid notificationId, Guid pendingCustomerUpdateId, string status, string templateId);

    [LoggerMessage(EventId = 82, Level = LogLevel.Error,
        Message = "Customer update email could not be sent; it stays unsent and the scheduler will retry. NotificationId={NotificationId} PendingCustomerUpdateId={PendingCustomerUpdateId} Status={Status} TemplateId={TemplateId}")]
    private static partial void LogSendFailed(ILogger logger, Guid notificationId, Guid pendingCustomerUpdateId, string status, string templateId, Exception exception);

    [LoggerMessage(EventId = 83, Level = LogLevel.Warning,
        Message = "Customer update email stays unsent: {Reason}. NotificationId={NotificationId} PendingCustomerUpdateId={PendingCustomerUpdateId}")]
    private static partial void LogSkipped(ILogger logger, Guid notificationId, Guid pendingCustomerUpdateId, string reason);

    [LoggerMessage(EventId = 84, Level = LogLevel.Information,
        Message = "Customer update email run complete. Unsent={Unsent} Sent={Sent} Skipped={Skipped} Failed={Failed}")]
    private static partial void LogRunSummary(ILogger logger, int unsent, int sent, int skipped, int failed);

    [LoggerMessage(EventId = 85, Level = LogLevel.Error,
        Message = "Customer update emails could not be sent after the decision was saved; the scheduler will retry. PendingCustomerUpdateId={PendingCustomerUpdateId}")]
    private static partial void LogImmediateSendFailed(ILogger logger, Guid pendingCustomerUpdateId, Exception exception);
}
