using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Core.Notifications;

namespace PTL.Core.Distribution;

// The Test Consultant emails of the Tabulations page - legacy ProficiencyTestingService email.vb
// ProcessTestConsultantEmails, sent through GOV.UK Notify instead of SMTP. EmailState moves only after
// Notify accepts the email, so a failed send is retried by the next scheduler run.
public interface ITabulationEmailService
{
    // The first email for distributions just sent to the Test Consultant, called after Save has committed.
    // Never throws (cancellation aside): a failure is logged and the scheduler retries it.
    Task SendFirstEmailsAsync(IReadOnlyList<Guid> monthlyDistributionSchemeIds, CancellationToken cancellationToken = default);

    // One scheduler run: first emails still unsent, and chasers.
    Task<TabulationEmailRunSummary> ProcessScheduledEmailsAsync(CancellationToken cancellationToken = default);
}

public sealed partial class TabulationEmailService(
    IDistributionRepository repository,
    INotifyClient notifyClient,
    TabulationEmailGuard guard,
    IOptions<TabulationEmailOptions> options,
    TabulationEmailContext context,
    ILogger<TabulationEmailService> logger) : ITabulationEmailService
{
    // Legacy <<WebAddress>> value.
    private const string WebAddressPrefix = "You can access the tabulation at the following address: ";

    private static readonly EmailTemplate FirstRequestTemplate =
        new(TabulationEmailType.FirstRequest, TabulationEmailTemplates.FirstRequestId, TabulationEmailTemplates.FirstRequestName);

    private static readonly EmailTemplate ChaserTemplate =
        new(TabulationEmailType.Chaser, TabulationEmailTemplates.ChaserId, TabulationEmailTemplates.ChaserName);

    private readonly record struct EmailTemplate(TabulationEmailType Type, string Id, string Name);

    private readonly record struct SendResult(int Sent, int Skipped, int Failed);

    // Null when nothing is due: comments are in, the email has already gone out, or the date is outside both windows.
    public static TabulationEmailType? DueStage(TabulationEmailCandidateEntity candidate, DateTime today)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.IsCommentSubmitted)
        {
            return null;
        }

        var daysSinceReady = (today - candidate.IsReadyForTestConsultant.Date).Days;
        var state = (TabulationEmailState)candidate.EmailState;

        if (daysSinceReady is >= 0 and <= TabulationEmailSchedule.FirstEmailLastDay && state == TabulationEmailState.None)
        {
            return TabulationEmailType.FirstRequest;
        }

        // A first email that never went out is replaced by the chaser, as in legacy.
        if (daysSinceReady >= TabulationEmailSchedule.ChaserFirstDay
            && daysSinceReady < TabulationEmailSchedule.OverdueDay
            && state != TabulationEmailState.ChaserEmailSent)
        {
            return TabulationEmailType.Chaser;
        }

        return null;
    }

    public async Task SendFirstEmailsAsync(IReadOnlyList<Guid> monthlyDistributionSchemeIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(monthlyDistributionSchemeIds);
        if (!options.Value.Enabled || monthlyDistributionSchemeIds.Count == 0)
        {
            return;
        }

        await guard.Lock.WaitAsync(cancellationToken);
        try
        {
            var today = Today();
            var wanted = monthlyDistributionSchemeIds.ToHashSet();
            var candidates = await repository.GetTabulationEmailCandidatesAsync(WindowStart(today), today.AddDays(1), cancellationToken);
            await SendDueAsync(candidates.Where(c => wanted.Contains(c.MonthlyDistributionSchemeId)), today, firstOnly: true, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogImmediateSendFailed(logger, ex);
        }
        finally
        {
            guard.Lock.Release();
        }
    }

    public async Task<TabulationEmailRunSummary> ProcessScheduledEmailsAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return TabulationEmailRunSummary.Empty;
        }

        await guard.Lock.WaitAsync(cancellationToken);
        try
        {
            var today = Today();
            var candidates = await repository.GetTabulationEmailCandidatesAsync(WindowStart(today), today.AddDays(1), cancellationToken);
            var summary = await SendDueAsync(candidates, today, firstOnly: false, cancellationToken);
            LogRunSummary(logger, candidates.Count, summary.Due, summary.FirstEmailsSent, summary.ChaserEmailsSent, summary.Skipped, summary.Failed);
            return summary;
        }
        finally
        {
            guard.Lock.Release();
        }
    }

    private DateTime Today() => context.Today();

    // The ready date of a distribution still inside the chaser window.
    private static DateTime WindowStart(DateTime today) => today.AddDays(-(TabulationEmailSchedule.OverdueDay - 1));

    private async Task<TabulationEmailRunSummary> SendDueAsync(
        IEnumerable<TabulationEmailCandidateEntity> candidates, DateTime today, bool firstOnly, CancellationToken cancellationToken)
    {
        Dictionary<Guid, TabulationConsultantEntity>? consultants = null;
        int due = 0, firstSent = 0, chasersSent = 0, skipped = 0, failed = 0;

        foreach (var candidate in candidates)
        {
            var stage = DueStage(candidate, today);
            if (stage is null || (firstOnly && stage != TabulationEmailType.FirstRequest))
            {
                continue;
            }

            due++;
            try
            {
                consultants ??= await LoadConsultantsAsync(cancellationToken);
                if (stage == TabulationEmailType.FirstRequest)
                {
                    var result = await SendFirstAsync(candidate, consultants, today, cancellationToken);
                    (firstSent, skipped, failed) = (firstSent + result.Sent, skipped + result.Skipped, failed + result.Failed);
                }
                else
                {
                    var result = await SendChaserAsync(candidate, consultants, today, cancellationToken);
                    (chasersSent, skipped, failed) = (chasersSent + result.Sent, skipped + result.Skipped, failed + result.Failed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                LogProcessingFailed(logger, stage.Value, candidate.SchemeId, candidate.MonthlyDistributionSchemeId, ex);
            }
        }

        return new TabulationEmailRunSummary(due, firstSent, chasersSent, skipped, failed);
    }

    private async Task<Dictionary<Guid, TabulationConsultantEntity>> LoadConsultantsAsync(CancellationToken cancellationToken)
    {
        var consultants = new Dictionary<Guid, TabulationConsultantEntity>();
        foreach (var consultant in await repository.GetTabulationEmailConsultantsAsync(cancellationToken))
        {
            consultants.TryAdd(consultant.UserId, consultant);
        }

        return consultants;
    }

    // Test Consultant 1 only. When the email went out but the state write failed, the retry writes the state only.
    private async Task<SendResult> SendFirstAsync(
        TabulationEmailCandidateEntity candidate, Dictionary<Guid, TabulationConsultantEntity> consultants, DateTime today, CancellationToken cancellationToken)
    {
        var id = candidate.MonthlyDistributionSchemeId;
        var sentNow = 0;

        if (!guard.FirstSent(id))
        {
            var consultant = Find(consultants, candidate.TestConsultant1Id);
            if (consultant is null || string.IsNullOrWhiteSpace(consultant.Email))
            {
                WarnMissingRecipient(candidate, TabulationEmailType.FirstRequest, 1, today);
                return new SendResult(0, 1, 0);
            }

            var sent = await TrySendAsync(
                candidate,
                FirstRequestTemplate,
                consultant.Email,
                Personalise(candidate, consultant, consultant.FriendlyName, TabulationEmailSchedule.FirstEmailDeadlineDays, today),
                $"{id}:first",
                cancellationToken);
            if (!sent)
            {
                return new SendResult(0, 0, 1);
            }

            guard.MarkFirstSent(id);
            sentNow = 1;
        }

        await repository.SetTabulationEmailStateAsync(id, (int)TabulationEmailState.FirstEmailSent, CancellationToken.None);
        guard.ClearFirst(id);
        return new SendResult(sentNow, 0, 0);
    }

    // One email per Test Consultant of the scheme. The state moves once every allocated consultant has been emailed.
    private async Task<SendResult> SendChaserAsync(
        TabulationEmailCandidateEntity candidate, Dictionary<Guid, TabulationConsultantEntity> consultants, DateTime today, CancellationToken cancellationToken)
    {
        // <<Commenter>> is Test Consultant 1; without them the email cannot be written (as in legacy).
        var commenter = Find(consultants, candidate.TestConsultant1Id);
        if (commenter is null)
        {
            WarnMissingRecipient(candidate, TabulationEmailType.Chaser, 1, today);
            return new SendResult(0, 1, 0);
        }

        int sent = 0, skipped = 0, failed = 0;
        var allEmailed = true;

        foreach (var (slot, consultantId) in new[] { (1, candidate.TestConsultant1Id), (2, candidate.TestConsultant2Id), (3, candidate.TestConsultant3Id) })
        {
            if (consultantId is null || consultantId == Guid.Empty || guard.ChaserSent(candidate.MonthlyDistributionSchemeId, slot))
            {
                continue;
            }

            var consultant = Find(consultants, consultantId);
            if (consultant is null || string.IsNullOrWhiteSpace(consultant.Email))
            {
                WarnMissingRecipient(candidate, TabulationEmailType.Chaser, slot, today);
                skipped++;
                allEmailed = false;
                continue;
            }

            var delivered = await TrySendAsync(
                candidate,
                ChaserTemplate,
                consultant.Email,
                Personalise(candidate, consultant, commenter.FriendlyName, TabulationEmailSchedule.ChaserDeadlineDays, today),
                $"{candidate.MonthlyDistributionSchemeId}:chaser:{slot}",
                cancellationToken);
            if (delivered)
            {
                guard.MarkChaserSent(candidate.MonthlyDistributionSchemeId, slot);
                sent++;
            }
            else
            {
                failed++;
                allEmailed = false;
            }
        }

        if (allEmailed)
        {
            await repository.SetTabulationEmailStateAsync(candidate.MonthlyDistributionSchemeId, (int)TabulationEmailState.ChaserEmailSent, CancellationToken.None);
            guard.ClearChasers(candidate.MonthlyDistributionSchemeId);
        }

        return new SendResult(sent, skipped, failed);
    }

    private async Task<bool> TrySendAsync(
        TabulationEmailCandidateEntity candidate,
        EmailTemplate template,
        string emailAddress,
        Dictionary<string, string> personalisation,
        string reference,
        CancellationToken cancellationToken)
    {
        try
        {
            await notifyClient.SendEmailAsync(template.Id, emailAddress, personalisation, reference, cancellationToken);
            LogSent(logger, template.Type, candidate.SchemeId, candidate.MonthlyDistributionSchemeId, template.Name, template.Id);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSendFailed(logger, template.Type, candidate.SchemeId, candidate.MonthlyDistributionSchemeId, template.Name, template.Id, ex);
            return false;
        }
    }

    // The placeholders of the legacy TestConsultantEmailBodyFile.txt.
    private Dictionary<string, string> Personalise(
        TabulationEmailCandidateEntity candidate, TabulationConsultantEntity recipient, string commenter, int deadlineDays, DateTime today) =>
        new()
        {
            ["DisplayName"] = string.IsNullOrEmpty(recipient.Department) ? recipient.FriendlyName : $"{recipient.FriendlyName}, {recipient.Department}",
            ["Commenter"] = commenter,
            ["SchemeName"] = candidate.SchemeName,
            ["MonthlyDistributionReferenceFull"] = candidate.ReferenceFull,
            ["CommentDeadlineDays"] = deadlineDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["WebAddress"] = WebAddressPrefix + context.CommentsLink(candidate.MonthlyDistributionSchemeId, recipient.IsExternal),
            ["DateTimeNow"] = TabulationEmailContext.Format(today),
        };

    private static TabulationConsultantEntity? Find(Dictionary<Guid, TabulationConsultantEntity> consultants, Guid? id) =>
        id is { } key && key != Guid.Empty && consultants.TryGetValue(key, out var consultant) ? consultant : null;

    private void WarnMissingRecipient(TabulationEmailCandidateEntity candidate, TabulationEmailType emailType, int slot, DateTime today)
    {
        if (guard.FirstWarningToday(candidate.MonthlyDistributionSchemeId, slot, today))
        {
            LogMissingRecipient(logger, emailType, candidate.SchemeId, candidate.MonthlyDistributionSchemeId, slot);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Tabulation email sent. EmailType={EmailType} SchemeId={SchemeId} MonthlyDistributionSchemeId={MonthlyDistributionSchemeId} TemplateName={TemplateName} TemplateId={TemplateId}")]
    private static partial void LogSent(ILogger logger, TabulationEmailType emailType, Guid schemeId, Guid monthlyDistributionSchemeId, string templateName, string templateId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "Tabulation email could not be sent; EmailState is unchanged and the scheduler will retry. EmailType={EmailType} SchemeId={SchemeId} MonthlyDistributionSchemeId={MonthlyDistributionSchemeId} TemplateName={TemplateName} TemplateId={TemplateId}")]
    private static partial void LogSendFailed(ILogger logger, TabulationEmailType emailType, Guid schemeId, Guid monthlyDistributionSchemeId, string templateName, string templateId, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Tabulation email has no recipient with an email address; EmailState is unchanged. EmailType={EmailType} SchemeId={SchemeId} MonthlyDistributionSchemeId={MonthlyDistributionSchemeId} TestConsultantSlot={Slot}")]
    private static partial void LogMissingRecipient(ILogger logger, TabulationEmailType emailType, Guid schemeId, Guid monthlyDistributionSchemeId, int slot);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error,
        Message = "Tabulation email processing failed. EmailType={EmailType} SchemeId={SchemeId} MonthlyDistributionSchemeId={MonthlyDistributionSchemeId}")]
    private static partial void LogProcessingFailed(ILogger logger, TabulationEmailType emailType, Guid schemeId, Guid monthlyDistributionSchemeId, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "The first Test Consultant emails could not be sent after Save; the scheduler will retry.")]
    private static partial void LogImmediateSendFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information,
        Message = "Tabulation email run complete. Candidates={Candidates} Due={Due} FirstEmailsSent={FirstEmailsSent} ChaserEmailsSent={ChaserEmailsSent} Skipped={Skipped} Failed={Failed}")]
    private static partial void LogRunSummary(ILogger logger, int candidates, int due, int firstEmailsSent, int chaserEmailsSent, int skipped, int failed);
}
