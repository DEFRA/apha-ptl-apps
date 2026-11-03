using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Configuration;
using PTL.Core.Notifications;
using PTL.Core.Participant;

namespace PTL.Core.Distribution;

public sealed record PostedParticipant(Guid ParticipantId, Guid MonthlyDistributionSchemeId, string Distribution, string SchemeName);

// Queued is every email the run was given. Skipped are the ones left unsent on purpose: the category is switched off or there is no address.
public sealed record DistributionPostedRunSummary(int Queued, int Sent, int Skipped, int Failed)
{
    public static DistributionPostedRunSummary Empty { get; } = new(0, 0, 0, 0);
}

// Distribution Posted emails - legacy email.vb ProcessDistributionPostedEmails. The rows of tblDistributionPostedEmail queued by the packaging
// save (spiDistributionPostedEmail) are the record of what has to go out: each is sent through GOV.UK Notify and marked sent only afterwards, so
// a failed send stays unsent. There is no retry.
public interface IDistributionPostedNotifier
{
    // Legacy DistributionPostedEmailCollection.AddNewDistributionPostedEmail: one email per
    // participant email address (primary, plus the secondary if there is one).
    Task<IReadOnlyList<DistributionPostedEmailEntity>> BuildEmailsAsync(
        IReadOnlyList<PostedParticipant> posted, CancellationToken cancellationToken = default);

    // Sends one save's emails through GOV.UK Notify (replaces legacy's polled SMTP service). A failed send is logged and stays unsent; it never
    // stops the others.
    Task<DistributionPostedRunSummary> SendAsync(
        IReadOnlyList<DistributionPostedEmailEntity> emails, CancellationToken cancellationToken = default);
}

// Hands the emails of a just-committed packaging save to the sender and returns at once, so the packaging user never waits for them.
public interface IDistributionPostedEmailDispatcher
{
    void Dispatch(IReadOnlyList<DistributionPostedEmailEntity> emails);
}

public sealed partial class DistributionPostedNotifier(
    IDistributionRepository repository,
    IParticipantService participantService,
    INotifyClient notifyClient,
    IOptions<DistributionPostedEmailOptions> options,
    IOptions<ExternalOptions> externalOptions,
    TimeProvider timeProvider,
    ILogger<DistributionPostedNotifier> logger) : IDistributionPostedNotifier
{
    public async Task<IReadOnlyList<DistributionPostedEmailEntity>> BuildEmailsAsync(
        IReadOnlyList<PostedParticipant> posted, CancellationToken cancellationToken = default)
    {
        if (posted.Count == 0)
        {
            return [];
        }

        var queued = new HashSet<(Guid ParticipantId, Guid MonthlyDistributionSchemeId, string Email)>();

        var emails = new List<DistributionPostedEmailEntity>();
        foreach (var item in posted)
        {
            var participant = await participantService.GetParticipantAsync(item.ParticipantId, cancellationToken);
            if (participant is null)
            {
                LogSkipped(logger, "participant not found", item.ParticipantId, item.MonthlyDistributionSchemeId);
                continue;
            }

            var addresses = new[] { participant.Email, participant.Email2 }.Where(address => !string.IsNullOrWhiteSpace(address)).ToList();
            if (addresses.Count == 0)
            {
                LogSkipped(logger, "no email address", item.ParticipantId, item.MonthlyDistributionSchemeId);
                continue;
            }

            foreach (var address in addresses)
            {
                // The same address twice for one participant and scheme (primary = secondary, or the row posted from two sheets) gets one email.
                if (!queued.Add((item.ParticipantId, item.MonthlyDistributionSchemeId, address.ToUpperInvariant())))
                {
                    continue;
                }

                emails.Add(new DistributionPostedEmailEntity
                {
                    DistributionPostedEmailId = Guid.NewGuid(),
                    ParticipantId = item.ParticipantId,
                    MonthlyDistributionSchemeId = item.MonthlyDistributionSchemeId,
                    Email = address,
                    Distribution = item.Distribution,
                    SchemeName = item.SchemeName,
                });
            }
        }

        return emails;
    }

    public async Task<DistributionPostedRunSummary> SendAsync(
        IReadOnlyList<DistributionPostedEmailEntity> emails, CancellationToken cancellationToken = default)
    {
        if (emails.Count == 0)
        {
            return DistributionPostedRunSummary.Empty;
        }

        if (!options.Value.Enabled)
        {
            LogSwitchedOff(logger, emails.Count);
            return new DistributionPostedRunSummary(emails.Count, 0, emails.Count, 0);
        }

        var outcomes = new List<EmailSendOutcome>(emails.Count);
        foreach (var email in emails)
        {
            outcomes.Add(await SendOneAsync(email, cancellationToken));
        }

        var summary = new DistributionPostedRunSummary(
            emails.Count,
            outcomes.Count(o => o == EmailSendOutcome.Sent),
            outcomes.Count(o => o == EmailSendOutcome.Skipped),
            outcomes.Count(o => o == EmailSendOutcome.Failed));
        LogRunSummary(logger, summary.Queued, summary.Sent, summary.Skipped, summary.Failed);
        return summary;
    }

    private async Task<EmailSendOutcome> SendOneAsync(DistributionPostedEmailEntity email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email.Email))
        {
            LogSkipped(logger, "no email address", email.ParticipantId, email.MonthlyDistributionSchemeId);
            return EmailSendOutcome.Skipped;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            await notifyClient.SendEmailAsync(DistributionPostedEmailTemplates.Id, email.Email, Personalise(email, now), email.DistributionPostedEmailId.ToString(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // A Notify timeout also surfaces as a cancellation; only the host stopping ends the whole run.
            LogSendFailed(logger, email.DistributionPostedEmailId, email.ParticipantId, email.MonthlyDistributionSchemeId, DistributionPostedEmailTemplates.Name, DistributionPostedEmailTemplates.Id, ex);
            return EmailSendOutcome.Failed;
        }

        await MarkSentAsync(email, now);
        LogSent(logger, email.DistributionPostedEmailId, email.ParticipantId, email.MonthlyDistributionSchemeId, DistributionPostedEmailTemplates.Name, DistributionPostedEmailTemplates.Id);
        return EmailSendOutcome.Sent;
    }

    // Notify already has the email, so a failure here is logged and the email still counts as sent. Not cancellable: stopping the host
    // must not leave a delivered email looking unsent.
    private async Task MarkSentAsync(DistributionPostedEmailEntity email, DateTime sentAt)
    {
        try
        {
            await repository.MarkDistributionPostedEmailSentAsync(email.DistributionPostedEmailId, sentAt, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogMarkSentFailed(logger, email.DistributionPostedEmailId, email.ParticipantId, email.MonthlyDistributionSchemeId, ex);
        }
    }

    // The placeholders of the legacy DistributionPostedEmailBodyFile.txt / SubjectFile.txt.
    private Dictionary<string, string> Personalise(DistributionPostedEmailEntity email, DateTime now) =>
        new()
        {
            ["scheme_name"] = email.SchemeName,
            ["distribution"] = email.Distribution,
            ["external_link"] = externalOptions.Value.AppUrl,
            ["date"] = now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        };

    [LoggerMessage(EventId = 111, Level = LogLevel.Information,
        Message = "Distribution Posted email sent. DistributionPostedEmailId={DistributionPostedEmailId} ParticipantId={ParticipantId} MonthlyDistributionSchemeId={MonthlyDistributionSchemeId} TemplateName={TemplateName} TemplateId={TemplateId}")]
    private static partial void LogSent(ILogger logger, Guid distributionPostedEmailId, Guid participantId, Guid monthlyDistributionSchemeId, string templateName, string templateId);

    [LoggerMessage(EventId = 112, Level = LogLevel.Error,
        Message = "Distribution Posted email could not be sent; it stays unsent. DistributionPostedEmailId={DistributionPostedEmailId} ParticipantId={ParticipantId} MonthlyDistributionSchemeId={MonthlyDistributionSchemeId} TemplateName={TemplateName} TemplateId={TemplateId}")]
    private static partial void LogSendFailed(ILogger logger, Guid distributionPostedEmailId, Guid participantId, Guid monthlyDistributionSchemeId, string templateName, string templateId, Exception exception);

    [LoggerMessage(EventId = 113, Level = LogLevel.Warning,
        Message = "Distribution Posted email skipped: {Reason}. ParticipantId={ParticipantId} MonthlyDistributionSchemeId={MonthlyDistributionSchemeId}")]
    private static partial void LogSkipped(ILogger logger, string reason, Guid participantId, Guid monthlyDistributionSchemeId);

    [LoggerMessage(EventId = 114, Level = LogLevel.Information,
        Message = "Distribution Posted email run complete. Queued={Queued} Sent={Sent} Skipped={Skipped} Failed={Failed}")]
    private static partial void LogRunSummary(ILogger logger, int queued, int sent, int skipped, int failed);

    [LoggerMessage(EventId = 115, Level = LogLevel.Information,
        Message = "Distribution Posted emails are switched off; they stay unsent. Queued={Queued}")]
    private static partial void LogSwitchedOff(ILogger logger, int queued);

    [LoggerMessage(EventId = 116, Level = LogLevel.Error,
        Message = "Distribution Posted email was sent but could not be marked sent; it must not be sent again. DistributionPostedEmailId={DistributionPostedEmailId} ParticipantId={ParticipantId} MonthlyDistributionSchemeId={MonthlyDistributionSchemeId}")]
    private static partial void LogMarkSentFailed(ILogger logger, Guid distributionPostedEmailId, Guid participantId, Guid monthlyDistributionSchemeId, Exception exception);
}
