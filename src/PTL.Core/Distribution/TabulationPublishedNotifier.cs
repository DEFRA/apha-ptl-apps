using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Notifications;

namespace PTL.Core.Distribution;

// Publication notification emails - legacy email.vb ProcessNotificationEmails. The rows of tblNotificationEmail
// (queued by Authorise with spiNotificationEmail) are the outbox: each unsent row of the tabulation just published is
// sent through GOV.UK Notify and marked sent only afterwards, so a failed send stays unsent.
public interface ITabulationPublishedNotifier
{
    Task<PublicationNotificationRunSummary> SendForPublishedTabulationAsync(Guid publishedTabulationId, CancellationToken cancellationToken = default);
}

// Hands a just-published tabulation to the sender and returns at once, so the sign-off user never waits for the emails.
public interface IPublicationEmailDispatcher
{
    void Dispatch(Guid publishedTabulationId);
}

public sealed partial class TabulationPublishedNotifier(
    IDistributionRepository repository,
    INotifyClient notifyClient,
    IOptions<PublicationNotificationOptions> options,
    TabulationEmailContext context,
    ILogger<TabulationPublishedNotifier> logger) : ITabulationPublishedNotifier
{
    public async Task<PublicationNotificationRunSummary> SendForPublishedTabulationAsync(Guid publishedTabulationId, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return PublicationNotificationRunSummary.Empty;
        }

        var today = context.Today();

        // Only this tabulation's rows: the table can still hold unsent rows from before the emails went out on Authorise.
        var unsent = (await repository.GetUnsentNotificationEmailsAsync(cancellationToken))
            .Where(email => email.PublishedTabulationId == publishedTabulationId)
            .ToList();
        var (server, address) = context.ExternalLocation();
        int sent = 0, skipped = 0, failed = 0;

        foreach (var email in unsent)
        {
            if (string.IsNullOrWhiteSpace(email.Email))
            {
                skipped++;
                LogMissingAddress(logger, nameof(TabulationEmailType.PublishedNotification), email.NotificationEmailId, email.PublishedTabulationId);
                continue;
            }

            try
            {
                await notifyClient.SendEmailAsync(
                    TabulationEmailTemplates.PublishedId,
                    email.Email,
                    Personalise(email, server, address, today),
                    email.NotificationEmailId.ToString(),
                    cancellationToken);
                await repository.MarkNotificationEmailSentAsync(email.NotificationEmailId, context.UtcNow.UtcDateTime, CancellationToken.None);
                sent++;
                LogSent(logger, nameof(TabulationEmailType.PublishedNotification), email.NotificationEmailId, email.PublishedTabulationId, TabulationEmailTemplates.PublishedName, TabulationEmailTemplates.PublishedId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                LogSendFailed(logger, nameof(TabulationEmailType.PublishedNotification), email.NotificationEmailId, email.PublishedTabulationId, TabulationEmailTemplates.PublishedName, TabulationEmailTemplates.PublishedId, ex);
            }
        }

        var summary = new PublicationNotificationRunSummary(unsent.Count, sent, skipped, failed);
        LogRunSummary(logger, publishedTabulationId, summary.Unsent, summary.Sent, summary.Skipped, summary.Failed);
        return summary;
    }

    // The placeholders of the legacy NotificationEmailBodyFile.txt.
    private static Dictionary<string, string> Personalise(NotificationEmailEntity email, string externalServer, string externalAddress, DateTime today) =>
        new()
        {
            ["DisplayName"] = email.Name,
            ["SchemeName"] = email.Scheme,
            ["Distribution"] = email.Distribution,
            ["ExternalServer"] = externalServer,
            ["ExternalAddress"] = externalAddress,
            ["PublishedTabulationId"] = email.PublishedTabulationId.ToString(),
            ["DateTimeNow"] = TabulationEmailContext.Format(today),
        };

    [LoggerMessage(EventId = 21, Level = LogLevel.Information,
        Message = "Publication notification sent. EmailType={EmailType} NotificationEmailId={NotificationEmailId} PublishedTabulationId={PublishedTabulationId} TemplateName={TemplateName} TemplateId={TemplateId}")]
    private static partial void LogSent(ILogger logger, string emailType, Guid notificationEmailId, Guid publishedTabulationId, string templateName, string templateId);

    [LoggerMessage(EventId = 22, Level = LogLevel.Error,
        Message = "Publication notification could not be sent; it stays unsent. EmailType={EmailType} NotificationEmailId={NotificationEmailId} PublishedTabulationId={PublishedTabulationId} TemplateName={TemplateName} TemplateId={TemplateId}")]
    private static partial void LogSendFailed(ILogger logger, string emailType, Guid notificationEmailId, Guid publishedTabulationId, string templateName, string templateId, Exception exception);

    [LoggerMessage(EventId = 23, Level = LogLevel.Warning,
        Message = "Publication notification has no email address and stays unsent. EmailType={EmailType} NotificationEmailId={NotificationEmailId} PublishedTabulationId={PublishedTabulationId}")]
    private static partial void LogMissingAddress(ILogger logger, string emailType, Guid notificationEmailId, Guid publishedTabulationId);

    [LoggerMessage(EventId = 24, Level = LogLevel.Information,
        Message = "Publication notification run complete. PublishedTabulationId={PublishedTabulationId} Unsent={Unsent} Sent={Sent} Skipped={Skipped} Failed={Failed}")]
    private static partial void LogRunSummary(ILogger logger, Guid publishedTabulationId, int unsent, int sent, int skipped, int failed);
}
