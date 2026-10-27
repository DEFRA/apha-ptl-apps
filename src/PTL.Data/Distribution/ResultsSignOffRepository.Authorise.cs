using System.Data;
using Dapper;
using PTL.Core.Distribution;
using Rows = PTL.Data.Distribution.TabulationReportRows;

namespace PTL.Data.Distribution;

// Legacy Results Sign-off BtnAuthorise: PublishTabulation.Execute (copy the draft into the published tables, queue the
// notification emails) then tab.Publish()/Save() (mark it authorised). Legacy ran it in a 30 minute TransactionScope; here it is
// one database transaction, taken while the tabulation row is locked so only one sign-off user can decide it.
public sealed partial class ResultsSignOffRepository
{
    private const string DraftSql = "EXEC dbo.spgDistributionTabulation @DistributionTabulationId = @TabulationId";
    private const string SchemeSql = "EXEC dbo.spgMonthlyDistributionScheme @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";
    private const string IntendedResultsSql = "EXEC dbo.spgIntendedResults @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";
    private const string RecipientsSql = "EXEC dbo.sppNotificationEmails @DistributionTabulationId = @DistributionTabulationId";
    private const string QueueEmailSql = "EXEC dbo.spiNotificationEmail @NotificationEmailId = @NotificationEmailId, @PublishedTabulationId = @PublishedTabulationId, @Email = @Email, @Name = @Name";

    private const string SignatorySql = "SELECT fldFirstName, fldLastName, fldUsername FROM tblUsers WHERE fldUserId = @UserId";

    public async Task<ResultsSignOffOutcome> AuthoriseAsync(Guid tabulationId, Guid? signatoryUserId, DateTime creationDate, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var outcome = await AuthoriseAsync(connection, transaction, tabulationId, signatoryUserId, creationDate);
            if (outcome == ResultsSignOffOutcome.Done)
            {
                transaction.Commit();
            }
            else
            {
                transaction.Rollback();
            }

            return outcome;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<ResultsSignOffOutcome> AuthoriseAsync(IDbConnection connection, IDbTransaction transaction, Guid tabulationId, Guid? signatoryUserId, DateTime creationDate)
    {
        var state = await connection.ExecuteScalarAsync<int?>(StateSql, new { DistributionTabulationId = tabulationId }, transaction);
        if (state is null)
        {
            return ResultsSignOffOutcome.NotFound;
        }

        if (state != ResultsSignOffState.AwaitingSignOff)
        {
            return ResultsSignOffOutcome.NotAwaiting;
        }

        var signatory = await GetSignatoryAsync(connection, transaction, signatoryUserId);
        if (signatory is null)
        {
            return ResultsSignOffOutcome.NoSignatory;
        }

        var source = await ReadPublishSourceAsync(connection, transaction, tabulationId);
        if (source is null)
        {
            return ResultsSignOffOutcome.NotFound;
        }

        var plan = TabulationPublishPlanner.Plan(source, signatory, creationDate);
        foreach (var command in plan.Commands)
        {
            var parameters = new DynamicParameters();
            foreach (var (name, value) in command.Parameters)
            {
                // Only the optional dates are ever null (legacy's SmartDate.DBValue).
                parameters.Add(name, value, value is null ? DbType.DateTime : null);
            }

            await connection.ExecuteAsync(command.CommandText, parameters, transaction);
        }

        await QueueNotificationEmailsAsync(connection, transaction, tabulationId, plan.PublishedTabulationId);

        await connection.ExecuteAsync(
            SetStateSql,
            new { DistributionTabulationId = tabulationId, PublishState = ResultsSignOffState.Authorized },
            transaction);
        return ResultsSignOffOutcome.Done;
    }

    // Legacy signed with the user's first and last name, or with the account name when the user list had no match.
    private static async Task<string?> GetSignatoryAsync(IDbConnection connection, IDbTransaction transaction, Guid? userId)
    {
        if (userId is null)
        {
            return null;
        }

        var user = await connection.QueryFirstOrDefaultAsync<Rows.PublishSignatory>(SignatorySql, new { UserId = userId }, transaction);
        if (user is null)
        {
            return null;
        }

        var name = $"{user.FirstName} {user.LastName}".Trim();
        return name.Length > 0 ? name : user.Username ?? string.Empty;
    }

    // Participants (with a second address when there is one), then the scheme's viewers, then the participants' viewers - one
    // email each, once per address and name. Legacy took the address from the SSO web service when the user was found there;
    // that service is gone, so the address held in PT-LIMS is used.
    private static async Task QueueNotificationEmailsAsync(IDbConnection connection, IDbTransaction transaction, Guid tabulationId, Guid publishedTabulationId)
    {
        List<Rows.PublishRecipient> participants, schemeViewers, participantViewers;
        using (var grid = await connection.QueryMultipleAsync(RecipientsSql, new { DistributionTabulationId = tabulationId }, transaction))
        {
            participants = (await grid.ReadAsync<Rows.PublishRecipient>()).ToList();
            schemeViewers = (await grid.ReadAsync<Rows.PublishRecipient>()).ToList();
            participantViewers = (await grid.ReadAsync<Rows.PublishRecipient>()).ToList();
        }

        var queued = new HashSet<(string Email, string Name)>();
        var recipients = new List<(string Email, string Name)>();

        void Add(string? email, string? name)
        {
            var recipient = (email ?? string.Empty, name ?? string.Empty);
            if (queued.Add(recipient))
            {
                recipients.Add(recipient);
            }
        }

        foreach (var participant in participants)
        {
            Add(participant.Email, participant.Name);
            if (!string.IsNullOrEmpty(participant.Email2))
            {
                Add(participant.Email2, participant.Name);
            }
        }

        foreach (var viewer in schemeViewers.Concat(participantViewers))
        {
            Add(viewer.Email, viewer.Name);
        }

        foreach (var (email, name) in recipients)
        {
            await connection.ExecuteAsync(
                QueueEmailSql,
                new { NotificationEmailId = Guid.NewGuid(), PublishedTabulationId = publishedTabulationId, Email = email, Name = name },
                transaction);
        }
    }

    // ResultsTabulation.Tabulation (spgDistributionTabulation), MonthlyDistributionScheme (spgMonthlyDistributionScheme) and
    // IntendedResultsSet (spgIntendedResults), exactly as legacy PublishTabulation read them.
    private static async Task<TabulationPublishSource?> ReadPublishSourceAsync(IDbConnection connection, IDbTransaction transaction, Guid tabulationId)
    {
        DraftRows draft;
        using (var grid = await connection.QueryMultipleAsync(DraftSql, new { TabulationId = tabulationId }, transaction))
        {
            var header = await grid.ReadFirstOrDefaultAsync<Rows.DraftHeader>();
            if (header is null)
            {
                return null;
            }

            draft = new DraftRows(header)
            {
                Tests = (await grid.ReadAsync<Rows.DraftTest>()).ToList(),
                MethodItems = (await grid.ReadAsync<Rows.DraftMethodItem>()).ToList(),
                ResultItems = (await grid.ReadAsync<Rows.DraftResultItem>()).ToList(),
                CategoryItems = (await grid.ReadAsync<Rows.DraftCategoryItem>()).ToList(),
                CriterionItems = (await grid.ReadAsync<Rows.DraftCriterionItem>()).ToList(),
                Samples = (await grid.ReadAsync<Rows.DraftSample>()).ToList(),
                Participants = (await grid.ReadAsync<Rows.DraftParticipant>()).ToList(),
                Ratings = (await grid.ReadAsync<Rows.DraftRating>()).ToList(),
                ParticipantMethods = (await grid.ReadAsync<Rows.DraftParticipantMethod>()).ToList(),
                ParticipantResults = (await grid.ReadAsync<Rows.DraftParticipantResult>()).ToList(),
                ParticipantCategories = (await grid.ReadAsync<Rows.DraftParticipantCategory>()).ToList(),
                ParticipantCriteria = (await grid.ReadAsync<Rows.DraftParticipantCriterion>()).ToList(),
                IntendedMethods = (await grid.ReadAsync<Rows.DraftIntendedMethod>()).ToList(),
                IntendedResults = (await grid.ReadAsync<Rows.DraftIntendedResult>()).ToList(),
            };

            // The two viewer sets are not part of what is published.
            _ = await grid.ReadAsync();
            _ = await grid.ReadAsync();

            draft.Assessments = (await grid.ReadAsync<Rows.DraftAssessment>()).ToList();
            draft.Attachments = (await grid.ReadAsync<Rows.DraftAttachment>()).ToList();
        }

        var schemeRow = await connection.QueryFirstOrDefaultAsync<Rows.PublishScheme>(
            SchemeSql, new { draft.Header.MonthlyDistributionSchemeId }, transaction);

        List<Rows.PublishSample> samples;
        using (var grid = await connection.QueryMultipleAsync(IntendedResultsSql, new { draft.Header.MonthlyDistributionSchemeId }, transaction))
        {
            _ = await grid.ReadAsync();
            samples = (await grid.ReadAsync<Rows.PublishSample>()).ToList();
        }

        return ToSource(draft, schemeRow ?? new Rows.PublishScheme(), samples);
    }

    private sealed class DraftRows(Rows.DraftHeader header)
    {
        public Rows.DraftHeader Header { get; } = header;
        public List<Rows.DraftTest> Tests { get; init; } = [];
        public List<Rows.DraftMethodItem> MethodItems { get; init; } = [];
        public List<Rows.DraftResultItem> ResultItems { get; init; } = [];
        public List<Rows.DraftCategoryItem> CategoryItems { get; init; } = [];
        public List<Rows.DraftCriterionItem> CriterionItems { get; init; } = [];
        public List<Rows.DraftSample> Samples { get; init; } = [];
        public List<Rows.DraftParticipant> Participants { get; init; } = [];
        public List<Rows.DraftRating> Ratings { get; init; } = [];
        public List<Rows.DraftParticipantMethod> ParticipantMethods { get; init; } = [];
        public List<Rows.DraftParticipantResult> ParticipantResults { get; init; } = [];
        public List<Rows.DraftParticipantCategory> ParticipantCategories { get; init; } = [];
        public List<Rows.DraftParticipantCriterion> ParticipantCriteria { get; init; } = [];
        public List<Rows.DraftIntendedMethod> IntendedMethods { get; init; } = [];
        public List<Rows.DraftIntendedResult> IntendedResults { get; init; } = [];
        public List<Rows.DraftAssessment> Assessments { get; set; } = [];
        public List<Rows.DraftAttachment> Attachments { get; set; } = [];
    }

    private static TabulationPublishSource ToSource(DraftRows draft, Rows.PublishScheme scheme, List<Rows.PublishSample> samples)
    {
        var h = draft.Header;

        var header = new PublishHeader(
            h.DistributionTabulationId,
            h.MonthlyDistributionSchemeId,
            h.TabulationName ?? string.Empty,
            h.SchemeName ?? string.Empty,
            h.SchemeIdentifier ?? string.Empty,
            h.RequiresAssessment ?? false,
            h.DistributionReference ?? string.Empty,
            h.DistributionReferenceSuffix ?? string.Empty,
            h.ScheduleCode ?? string.Empty,
            h.Comments ?? string.Empty,
            h.TestConsultantComments ?? string.Empty,
            h.TestConsultantCommentsAuthor ?? string.Empty,
            h.TestConsultantCommentsDept ?? string.Empty,
            h.DistributionDate ?? default,
            h.SchemeVersionDate ?? default,
            h.HasRatings ?? false,
            h.IntendedResultsOnly ?? false,
            h.SingleParticipantTabulation ?? false,
            h.AvailableToParticipants ?? false,
            h.AvailableToViewers ?? false,
            h.StandardTabulationText ?? string.Empty);

        var criteriaByCategory = draft.CriterionItems.ToLookup(c => (c.TestId, c.CategoryItemId));
        var tests = draft.Tests.Select(t => new PublishTest(
            t.TestId,
            t.TestType ?? string.Empty,
            t.AssessmentSubmitted,
            t.AssessorId ?? Guid.Empty,
            t.AssessorName ?? string.Empty,
            [.. draft.Samples.Where(s => s.TestId == t.TestId).Select(s => new PublishTestSample(s.SampleId, s.SampleTestId))],
            [.. draft.ResultItems.Where(r => r.TestId == t.TestId).Select(r => new PublishNamedItem(r.TestResultItemId, r.TestResultItemType ?? string.Empty))],
            [.. draft.MethodItems.Where(m => m.TestId == t.TestId).Select(m => new PublishNamedItem(m.TestMethodItemId, m.TestMethodItemType ?? string.Empty))],
            [.. draft.CategoryItems.Where(c => c.TestId == t.TestId).Select(c => new PublishCategoryItem(
                c.CategoryItemId,
                c.Name ?? string.Empty,
                [.. criteriaByCategory[(t.TestId, c.CategoryItemId)].Select(i => new PublishNamedItem(i.CriterionItemId, i.Name ?? string.Empty))]))])).ToList();

        var participantCriteria = draft.ParticipantCriteria.ToLookup(c => (c.ParticipantId, c.TestId, c.SampleId, c.CategoryItemId));
        var participants = draft.Participants.Select(p => new PublishParticipant(
            p.DistributionParticipantId,
            p.LabCode ?? string.Empty,
            p.NilReturnReason ?? string.Empty,
            [.. draft.Ratings.Where(r => r.ParticipantId == p.ParticipantId).Select(r => new PublishRating(r.SampleId, r.Rating ?? string.Empty))],
            [.. draft.ParticipantMethods.Where(m => m.ParticipantId == p.ParticipantId).Select(m => new PublishMethodResult(m.TestMethodItemId, m.AmendedResult ?? string.Empty))],
            [.. draft.ParticipantResults.Where(r => r.ParticipantId == p.ParticipantId).Select(r => new PublishResultItemResult(r.TestResultItemId, r.SampleId, r.AmendedResult ?? string.Empty))],
            [.. draft.Assessments.Where(a => a.ParticipantId == p.ParticipantId).Select(a => new PublishAssessment(
                a.SampleTestId,
                a.SampleReceiptId,
                a.AssessmentComment ?? string.Empty,
                a.AssessorId ?? Guid.Empty,
                a.AssessorName ?? string.Empty,
                a.AssessmentDate,
                a.Score ?? 0,
                a.SubmissionDate,
                a.NonSubmissionDate,
                a.NonCapableDate))],
            [.. draft.ParticipantCategories.Where(c => c.ParticipantId == p.ParticipantId).Select(c => new PublishCategoryResult(
                c.CategoryItemId,
                c.SampleId,
                c.CategoryScore ?? 0,
                [.. participantCriteria[(p.ParticipantId, c.TestId, c.SampleId, c.CategoryItemId)].Select(i => i.CriterionItemId)]))])).ToList();

        return new TabulationPublishSource(
            header,
            new PublishScheme(scheme.MonthId ?? 0, scheme.YearId ?? 0, scheme.IsAccredited ?? false, scheme.RequiresAssessment ?? false),
            [.. samples.Select(s => new PublishSample(s.SampleId, s.SampleNumber ?? string.Empty, s.TabulationDescription ?? string.Empty))],
            tests,
            participants,
            [.. draft.IntendedMethods.Select(m => new PublishIntendedMethod(m.TestMethodItemId, m.IntendedResult ?? string.Empty))],
            [.. draft.IntendedResults.Select(r => new PublishIntendedResult(r.TestResultItemId, r.SampleId, r.IntendedResult ?? string.Empty, r.IntendedResult2 ?? string.Empty))],
            [.. draft.Attachments.Select(a => new PublishAttachment(a.MonthlyDistributionAttachmentId, a.FileName ?? string.Empty, a.ContentType ?? string.Empty, a.FileData ?? [], a.UploadDate))]);
    }
}
