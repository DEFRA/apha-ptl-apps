using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Notifications;

namespace PTL.Core.Customer;

// Customer details update emails - legacy email.vb ProcessUpdateCustomerDetailsEmails. The rows of
// tblUpdatedCustomerDetailsEmail queued by a decision are the record of what still has to go out: each is sent through
// GOV.UK Notify and marked sent only afterwards, so a failed send stays unsent. There is no retry.
public interface ICustomerUpdateNotifier
{
    // Sends one decided update's emails straight after the decision was saved. Never throws: whatever fails stays unsent.
    Task SendForUpdateAsync(Guid pendingCustomerUpdateId, CancellationToken cancellationToken = default);
}

public sealed partial class CustomerUpdateNotifier(
    IPendingCustomerUpdateRepository repository,
    INotifyClient notifyClient,
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

    private async Task SendAsync(Guid pendingCustomerUpdateId, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var unsent = (await repository.GetUnsentNotificationsAsync(cancellationToken))
            .Where(e => e.PendingCustomerUpdateId == pendingCustomerUpdateId)
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

    private async Task<EmailSendOutcome> SendOneAsync(CustomerUpdateEmailEntity email, DateTime now, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email.Email))
        {
            LogSkipped(logger, email.Id, email.PendingCustomerUpdateId, "no email address");
            return EmailSendOutcome.Skipped;
        }

        var templateId = email.IsApproved ? CustomerUpdateEmailTemplates.AcceptedId : CustomerUpdateEmailTemplates.DeclinedId;
        var status = email.IsApproved ? "Accepted" : "Declined";
        try
        {
            await notifyClient.SendEmailAsync(templateId, email.Email, Personalise(email, status), email.Id.ToString(), cancellationToken);
            await repository.MarkNotificationSentAsync(email.Id, now, CancellationToken.None);
            LogSent(logger, email.Id, email.PendingCustomerUpdateId, status, templateId);
            return EmailSendOutcome.Sent;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSendFailed(logger, email.Id, email.PendingCustomerUpdateId, status, templateId, ex);
            return EmailSendOutcome.Failed;
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
        Message = "Customer update email could not be sent; it stays unsent. NotificationId={NotificationId} PendingCustomerUpdateId={PendingCustomerUpdateId} Status={Status} TemplateId={TemplateId}")]
    private static partial void LogSendFailed(ILogger logger, Guid notificationId, Guid pendingCustomerUpdateId, string status, string templateId, Exception exception);

    [LoggerMessage(EventId = 83, Level = LogLevel.Warning,
        Message = "Customer update email stays unsent: {Reason}. NotificationId={NotificationId} PendingCustomerUpdateId={PendingCustomerUpdateId}")]
    private static partial void LogSkipped(ILogger logger, Guid notificationId, Guid pendingCustomerUpdateId, string reason);

    [LoggerMessage(EventId = 84, Level = LogLevel.Information,
        Message = "Customer update email run complete. Unsent={Unsent} Sent={Sent} Skipped={Skipped} Failed={Failed}")]
    private static partial void LogRunSummary(ILogger logger, int unsent, int sent, int skipped, int failed);

    [LoggerMessage(EventId = 85, Level = LogLevel.Error,
        Message = "Customer update emails could not be sent after the decision was saved; the unsent ones stay unsent. PendingCustomerUpdateId={PendingCustomerUpdateId}")]
    private static partial void LogImmediateSendFailed(ILogger logger, Guid pendingCustomerUpdateId, Exception exception);
}
