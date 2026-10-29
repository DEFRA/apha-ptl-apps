using System.Data;
using Dapper;
using PTL.Core.Audit;
using PTL.Core.Distribution;
using PTL.Data.Audit;
using Rows = PTL.Data.Distribution.TabulationReportRows;

namespace PTL.Data.Distribution;

// Legacy Assessor AssessmentEntry (spgAssessmentEntry, spgParticipantResultForAssessment) and its Save: DataPortal_Update with
// spiAuditAssessmentItem, spuAssessmentEntry and the category and criterion procedures, as one transaction. The log rows for the
// category and criterion changes go to the existing tblLog (spiLog) in the same transaction, so a failed audit write rolls the
// whole save back.
public sealed partial class AssessorRepository
{
    private const string EntrySql = "EXEC dbo.spgAssessmentEntry @ParticipantId = @ParticipantId, @SampleTestId = @SampleTestId";
    private const string ResultsSql = "EXEC dbo.spgParticipantResultForAssessment @SampleTestId = @SampleTestId, @ParticipantId = @ParticipantId";

    // The submissions the Assessment Items list shows (an applicable sample the participant submitted) of a scheme that is not
    // cancelled. IsAssessor is 1 when the user is one of the scheme's four Assessors.
    private const string EntryAccessSql = """
        SELECT
            CASE WHEN @AssessorId IN (s.fldAssessor1, s.fldAssessor2, s.fldAssessor3, s.fldAssessor4) THEN 1 ELSE 0 END AS fldIsAssessor,
            dt.fldTestId AS fldTestId
        FROM tblSampleReceipt sr
        INNER JOIN tblSampleTest st ON st.fldSampleTestId = sr.fldSampleTestId
        INNER JOIN tblDistributionTest dt ON dt.fldTestId = st.fldTestId
        INNER JOIN tblMonthlyDistributionScheme mds ON dt.fldDistributionSchemeId = mds.fldMonthlyDistributionSchemeId
        INNER JOIN tblScheme s ON s.fldSchemeId = mds.fldSchemeId
        WHERE sr.fldDistributionParticipantId = @ParticipantId
            AND sr.fldSampleTestId = @SampleTestId
            AND st.fldIsApplicable = 1
            AND sr.fldSubmissionDate IS NOT NULL
            AND mds.fldIsCancelled = 0
        """;

    // Locks the test row so the check and the writes below see the same state, and Submit waits for the save.
    private const string SubmittedSql = "SELECT fldAssessmentSubmitted FROM tblDistributionTest WITH (UPDLOCK, HOLDLOCK) WHERE fldTestId = @TestId";

    private const string UsernameSql = "SELECT fldUsername FROM tblUsers WHERE fldUserId = @UserId";

    // Legacy AuditActorResolver's fallback when the user cannot be named.
    private const string UnknownAssessor = "Guest";

    private const string LogPrefix = "Assessor";

    public async Task<AssessorAccess> GetEntryAccessAsync(Guid sampleTestId, Guid participantId, Guid assessorId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var access = await connection.QueryFirstOrDefaultAsync<Rows.AssessorEntryAccess>(
            EntryAccessSql, new { SampleTestId = sampleTestId, ParticipantId = participantId, AssessorId = assessorId });

        return ToAccess(access);
    }

    public async Task<AssessorEntryEntity?> GetEntryAsync(Guid sampleTestId, Guid participantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await ReadEntryAsync(connection, null, sampleTestId, participantId, withResults: true);
    }

    public async Task<AssessorSaveOutcome> SaveEntryAsync(AssessorEntrySave save, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(save);

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var outcome = await SaveAsync(connection, transaction, save, cancellationToken);
            if (outcome == AssessorSaveOutcome.Saved)
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

    private static async Task<AssessorSaveOutcome> SaveAsync(IDbConnection connection, IDbTransaction transaction, AssessorEntrySave save, CancellationToken cancellationToken)
    {
        var access = await connection.QueryFirstOrDefaultAsync<Rows.AssessorEntryAccess>(
            EntryAccessSql, new { save.SampleTestId, save.ParticipantId, AssessorId = save.AssessorUserId }, transaction);
        if (ToAccess(access) is var refused && refused != AssessorAccess.Granted)
        {
            return refused == AssessorAccess.Forbidden ? AssessorSaveOutcome.Forbidden : AssessorSaveOutcome.NotFound;
        }

        var submitted = await connection.ExecuteScalarAsync<DateTime?>(SubmittedSql, new { access!.TestId }, transaction);
        if (submitted is not null)
        {
            return AssessorSaveOutcome.Submitted;
        }

        var entry = await ReadEntryAsync(connection, transaction, save.SampleTestId, save.ParticipantId, withResults: false);
        if (entry is null)
        {
            return AssessorSaveOutcome.NotFound;
        }

        var username = await connection.ExecuteScalarAsync<string?>(UsernameSql, new { UserId = save.AssessorUserId }, transaction);
        var plan = AssessorEntryPlanner.Plan(entry, save, string.IsNullOrWhiteSpace(username) ? UnknownAssessor : username);

        foreach (var command in plan.Commands)
        {
            var parameters = new DynamicParameters();
            foreach (var (name, value) in command.Parameters)
            {
                parameters.Add(name, value);
            }

            await connection.ExecuteAsync(command.CommandText, parameters, transaction);
        }

        if (plan.CategoryChanges is { } changes)
        {
            await AuditTrailWriter.WriteAsync(
                connection,
                transaction,
                new AuditEntry(save.AssessorUserId, "SaveAssessmentEntry", "AssessmentEntry", entry.SampleReceiptId.ToString(), changes),
                LogPrefix,
                cancellationToken);
        }

        return AssessorSaveOutcome.Saved;
    }

    private static AssessorAccess ToAccess(Rows.AssessorEntryAccess? access) =>
        access switch
        {
            null => AssessorAccess.NotFound,
            { IsAssessor: 0 } => AssessorAccess.Forbidden,
            _ => AssessorAccess.Granted,
        };

    // Legacy AssessmentEntry.DataPortal_Fetch: the five result sets, then the participant's result items for the page.
    private static async Task<AssessorEntryEntity?> ReadEntryAsync(IDbConnection connection, IDbTransaction? transaction, Guid sampleTestId, Guid participantId, bool withResults)
    {
        Rows.AssessorEntryHeader? header;
        List<Rows.AssessorEntryCategory> categories;
        List<Rows.AssessorEntryActualCategory> actualCategories;
        List<Rows.AssessorEntryCriterion> criteria;
        List<Rows.AssessorEntryActualCriterion> actualCriteria;
        using (var results = await connection.QueryMultipleAsync(EntrySql, new { ParticipantId = participantId, SampleTestId = sampleTestId }, transaction))
        {
            header = await results.ReadFirstOrDefaultAsync<Rows.AssessorEntryHeader>();
            categories = (await results.ReadAsync<Rows.AssessorEntryCategory>()).ToList();
            actualCategories = (await results.ReadAsync<Rows.AssessorEntryActualCategory>()).ToList();
            criteria = (await results.ReadAsync<Rows.AssessorEntryCriterion>()).ToList();
            actualCriteria = (await results.ReadAsync<Rows.AssessorEntryActualCriterion>()).ToList();
        }

        if (header is null)
        {
            return null;
        }

        var entry = new AssessorEntryEntity
        {
            SampleReceiptId = header.SampleReceiptId,
            TestId = header.TestId,
            YearId = header.YearId,
            MonthId = header.MonthId,
            SchemeName = header.SchemeName ?? string.Empty,
            SchemeIdentifier = header.SchemeIdentifier ?? string.Empty,
            DistributionReference = header.DistributionReference ?? string.Empty,
            DistributionReferenceSuffix = header.DistributionReferenceSuffix ?? string.Empty,
            ScheduleCode = header.ScheduleCode ?? string.Empty,
            TestName = header.TestName ?? string.Empty,
            LabCode = header.Labcode ?? string.Empty,
            SampleNumber = header.SampleNumber ?? string.Empty,
            Comment = header.AssessmentComment ?? string.Empty,
            Score = header.Score ?? 0,
            AssessorId = header.AssessorId ?? Guid.Empty,
            AssessorName = header.AssessorName ?? string.Empty,
            AssessmentSubmitted = header.AssessmentSubmitted,
        };

        var savedScores = actualCategories.GroupBy(a => a.CategoryItemId).ToDictionary(g => g.Key, g => g.First().CategoryScore ?? 0);
        var criteriaByCategory = criteria.ToLookup(c => c.CategoryItemId);
        var ticked = actualCriteria.Select(a => a.CriterionItemId).ToHashSet();
        foreach (var category in categories)
        {
            var categoryEntity = new AssessorCategoryEntity
            {
                CategoryItemId = category.CategoryItemId,
                Name = category.Name ?? string.Empty,
                ActualScore = savedScores.TryGetValue(category.CategoryItemId, out var score) ? score : null,
            };
            categoryEntity.Criteria.AddRange(criteriaByCategory[category.CategoryItemId]
                .Select(c => new AssessorCriterionEntity(c.CriterionItemId, c.Name ?? string.Empty, ticked.Contains(c.CriterionItemId))));
            entry.Categories.Add(categoryEntity);
        }

        if (withResults)
        {
            var resultItems = await connection.QueryAsync<Rows.AssessorResultItem>(ResultsSql, new { SampleTestId = sampleTestId, ParticipantId = participantId }, transaction);
            entry.ResultItems.AddRange(resultItems.Select(r => new AssessorResultItemEntity(r.TestResultItemName ?? string.Empty, r.Result ?? string.Empty)));
        }

        return entry;
    }
}
