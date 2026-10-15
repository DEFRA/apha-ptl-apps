using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Configuration;
using PTL.Core.Notifications;

namespace PTL.Core.Distribution;

// Publication notification emails - legacy email.vb ProcessNotificationEmails. The rows of tblNotificationEmail
// (queued by the publish workflow with spiNotificationEmail) are the outbox: each unsent row is sent through
// GOV.UK Notify and marked sent only afterwards, so a failed send stays unsent for the next run.
public interface ITabulationPublishedNotifier
{
    Task<PublicationNotificationRunSummary> SendUnsentAsync(CancellationToken cancellationToken = default);
}

public sealed partial class TabulationPublishedNotifier(
    IDistributionRepository repository,
    INotifyClient notifyClient,
    TabulationEmailGuard guard,
    IOptions<PublicationNotificationOptions> options,
    IOptions<TabulationEmailOptions> emailOptions,
    IOptions<ExternalOptions> externalOptions,
    TimeProvider timeProvider,
    ILogger<TabulationPublishedNotifier> logger) : ITabulationPublishedNotifier
{
    public async Task<PublicationNotificationRunSummary> SendUnsentAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return PublicationNotificationRunSummary.Empty;
        }

        await guard.Lock.WaitAsync(cancellationToken);
        try
        {
            var today = TabulationEmailClock.Today(timeProvider, emailOptions.Value.TimeZone);
            var unsent = await repository.GetUnsentNotificationEmailsAsync(cancellationToken);
            var (server, address) = ExternalLocation();
            int sent = 0, skipped = 0, failed = 0;

            foreach (var email in unsent)
            {
                if (string.IsNullOrWhiteSpace(email.Email))
                {
                    skipped++;
                    if (guard.FirstWarningToday(email.NotificationEmailId, 0, today))
                    {
                        LogMissingAddress(logger, nameof(TabulationEmailType.PublishedNotification), email.NotificationEmailId, email.PublishedTabulationId);
                    }

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
                    await repository.MarkNotificationEmailSentAsync(email.NotificationEmailId, timeProvider.GetUtcNow().UtcDateTime, CancellationToken.None);
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
            LogRunSummary(logger, summary.Unsent, summary.Sent, summary.Skipped, summary.Failed);
            return summary;
        }
        finally
        {
            guard.Lock.Release();
        }
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
            ["DateTimeNow"] = TabulationEmailClock.Format(today),
        };

    // Legacy <<ExternalServer>>/<<ExternalAddress>> were two settings; here both come from External:AppUrl.
    private (string Server, string Address) ExternalLocation()
    {
        var appUrl = externalOptions.Value.AppUrl;
        return Uri.TryCreate(appUrl, UriKind.Absolute, out var uri)
            ? (uri.GetLeftPart(UriPartial.Authority), uri.AbsolutePath.Trim('/'))
            : (appUrl.TrimEnd('/'), string.Empty);
    }

    [LoggerMessage(EventId = 21, Level = LogLevel.Information,
        Message = "Publication notification sent. EmailType={EmailType} NotificationEmailId={NotificationEmailId} PublishedTabulationId={PublishedTabulationId} TemplateName={TemplateName} TemplateId={TemplateId}")]
    private static partial void LogSent(ILogger logger, string emailType, Guid notificationEmailId, Guid publishedTabulationId, string templateName, string templateId);

    [LoggerMessage(EventId = 22, Level = LogLevel.Error,
        Message = "Publication notification could not be sent; it stays unsent and the scheduler will retry. EmailType={EmailType} NotificationEmailId={NotificationEmailId} PublishedTabulationId={PublishedTabulationId} TemplateName={TemplateName} TemplateId={TemplateId}")]
    private static partial void LogSendFailed(ILogger logger, string emailType, Guid notificationEmailId, Guid publishedTabulationId, string templateName, string templateId, Exception exception);

    [LoggerMessage(EventId = 23, Level = LogLevel.Warning,
        Message = "Publication notification has no email address and stays unsent. EmailType={EmailType} NotificationEmailId={NotificationEmailId} PublishedTabulationId={PublishedTabulationId}")]
    private static partial void LogMissingAddress(ILogger logger, string emailType, Guid notificationEmailId, Guid publishedTabulationId);

    [LoggerMessage(EventId = 24, Level = LogLevel.Information,
        Message = "Publication notification run complete. Unsent={Unsent} Sent={Sent} Skipped={Skipped} Failed={Failed}")]
    private static partial void LogRunSummary(ILogger logger, int unsent, int sent, int skipped, int failed);
}
