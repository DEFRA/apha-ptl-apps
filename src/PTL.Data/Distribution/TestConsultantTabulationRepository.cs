using Dapper;
using PTL.Core.Distribution;
using PTL.Data.Infrastructure;
using Rows = PTL.Data.Distribution.TabulationReportRows;

namespace PTL.Data.Distribution;

// Test Consultant side of the comments: legacy YearInfoCollection/MonthlyDistributionInfoCollection.FetchCollection and
// Test Consultant MonthlyDistributionScheme.FetchMonthlyDistributionScheme. @ReferenceDate is left to its NULL default -
// the procedures then use today's date (legacy only overrides it from the ResultsTestDate test-harness appSetting).
public sealed class TestConsultantTabulationRepository(IDbConnectionFactory connectionFactory) : ITestConsultantTabulationRepository
{
    public async Task<IReadOnlyList<TestConsultantDistributionEntity>> GetDistributionsAsync(Guid testConsultantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgMonthlyDistributionInfoForTestConsultant @TestConsultantId = @TestConsultantId",
            new { TestConsultantId = testConsultantId });

        // The first result set (the years) is derived from the schemes, so it is not needed.
        _ = await results.ReadAsync();
        var months = (await results.ReadAsync<Rows.TestConsultantMonth>()).ToList();
        var schemes = (await results.ReadAsync<Rows.TestConsultantScheme>()).ToList();

        var monthOrder = months
            .Select((month, index) => (month.MonthlyDistributionId, month.MonthId, Index: index))
            .ToDictionary(m => m.MonthlyDistributionId, m => (m.MonthId, m.Index));

        // Months come newest first from the procedure; schemes keep its reference order within a month.
        return [.. schemes
            .Where(s => monthOrder.ContainsKey(s.MonthlyDistributionId))
            .OrderBy(s => monthOrder[s.MonthlyDistributionId].Index)
            .Select(s => new TestConsultantDistributionEntity
            {
                YearId = s.YearId,
                MonthId = monthOrder[s.MonthlyDistributionId].MonthId,
                MonthlyDistributionSchemeId = s.MonthlyDistributionSchemeId,
                SchemeName = s.SchemeName ?? string.Empty,
                SchemeIdentifier = s.SchemeIdentifier ?? string.Empty,
                DistributionReference = s.DistributionReference ?? string.Empty,
                DistributionReferenceSuffix = s.DistributionReferenceSuffix ?? string.Empty,
                ScheduleCode = s.ScheduleCode ?? string.Empty,
                TargetDate = s.ResultsIssueTargetDate ?? default,
                IsCommentSubmitted = s.IsCommentSubmitted ?? false,
            })];
    }

    public async Task<TestConsultantTabulationEntity?> GetTabulationAsync(Guid monthlyDistributionSchemeId, bool fullTabulation, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(
            "EXEC dbo.spgMonthlyDistributionSchemeForTestConsultant @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId, @GetFullTabulation = @GetFullTabulation",
            new { MonthlyDistributionSchemeId = monthlyDistributionSchemeId, GetFullTabulation = fullTabulation });

        var header = await results.ReadFirstOrDefaultAsync<Rows.TestConsultantHeader>();
        if (header is null)
        {
            return null;
        }

        var source = new TabulationSource
        {
            TabulationId = header.MonthlyDistributionSchemeId,
            DistributionTabulationId = header.MonthlyDistributionSchemeId,
            Distribution = $"{header.DistributionReference}{header.DistributionReferenceSuffix}/{header.ScheduleCode}",
            SchemeIdentifier = header.SchemeIdentifier ?? string.Empty,
            SchemeName = header.SchemeName ?? string.Empty,
            Comments = header.Comments ?? string.Empty,
            TestConsultantComments = header.TestConsultantComments ?? string.Empty,
            TestConsultantCommentsAuthor = header.TestConsultantCommentsAuthor ?? string.Empty,
            DistributionDate = header.DistributionDate ?? default,
            HasRatings = header.ShowRatings ?? false,
            StandardTabulationText = header.StandardTabulationText ?? string.Empty,
        };

        var entity = new TestConsultantTabulationEntity { Source = source, IsCommentSubmitted = header.IsCommentSubmitted ?? false };

        var tests = new Dictionary<Guid, TabulationSourceTest>();
        foreach (var row in await results.ReadAsync<Rows.DraftTest>())
        {
            var test = new TabulationSourceTest { TestId = row.TestId, Name = row.TestType ?? string.Empty };
            tests[row.TestId] = test;
            source.Tests.Add(test);
        }

        foreach (var row in await results.ReadAsync<Rows.DraftMethodItem>())
        {
            tests.GetValueOrDefault(row.TestId)?.MethodItems.Add(new(row.TestMethodItemId, row.TestMethodItemType ?? string.Empty));
        }

        foreach (var row in await results.ReadAsync<Rows.DraftResultItem>())
        {
            tests.GetValueOrDefault(row.TestId)?.ResultItems.Add(new(row.TestResultItemId, row.TestResultItemType ?? string.Empty));
        }

        foreach (var row in await results.ReadAsync<Rows.DraftSample>())
        {
            tests.GetValueOrDefault(row.TestId)?.Samples.Add(new(row.SampleId, null, row.SampleNumber ?? string.Empty, row.TabulationDescription ?? string.Empty));
        }

        await ReadParticipantsAsync(results, entity);

        foreach (var row in await results.ReadAsync<Rows.DraftIntendedMethod>())
        {
            source.IntendedMethodResults.TryAdd(row.TestMethodItemId, row.IntendedResult ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.DraftIntendedResult>())
        {
            source.IntendedResultItemResults.TryAdd((row.SampleId, row.TestResultItemId), (row.IntendedResult ?? string.Empty, row.IntendedResult2 ?? string.Empty));
        }

        foreach (var row in await results.ReadAsync<Rows.TestConsultantSampleDetail>())
        {
            entity.SampleDetails.Add(new TestConsultantSampleInformation(
                row.SampleNumber ?? string.Empty, row.TabulationDescription ?? string.Empty, row.TestConsultantsDescription ?? string.Empty));
        }

        return entity;
    }

    public async Task<TestConsultantSchemeState?> GetSchemeStateAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<Rows.TestConsultantSchemeState>(
            """
            SELECT
                s.fldTestConsultant1 AS fldTestConsultant1Id,
                s.fldTestConsultant2 AS fldTestConsultant2Id,
                s.fldTestConsultant3 AS fldTestConsultant3Id,
                mds.fldIsCommentSubmitted AS fldIsCommentSubmitted,
                mds.fldTestConsultantComments AS fldTestConsultantComments,
                mds.fldTestConsultantCommentsAuthorId AS fldTestConsultantCommentsAuthorId
            FROM tblMonthlyDistributionScheme mds
            INNER JOIN tblScheme s ON mds.fldSchemeId = s.fldSchemeId
            WHERE mds.fldMonthlyDistributionSchemeId = @MonthlyDistributionSchemeId
            """,
            new { MonthlyDistributionSchemeId = monthlyDistributionSchemeId });

        return row is null
            ? null
            : new TestConsultantSchemeState(
                row.TestConsultant1Id,
                row.TestConsultant2Id,
                row.TestConsultant3Id,
                row.IsCommentSubmitted ?? false,
                row.TestConsultantComments ?? string.Empty,
                row.TestConsultantCommentsAuthorId ?? Guid.Empty);
    }

    public async Task<string?> GetTestConsultantNameAsync(Guid testConsultantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<string?>(
            "SELECT dbo.fnGetTestConsultantNameById(@TestConsultantId)",
            new { TestConsultantId = testConsultantId });
    }

    private const string IsInactiveSql =
        "SELECT CASE WHEN EXISTS (SELECT 1 FROM tblUsers WHERE fldUserId = @TestConsultantId AND fldIsInactive = 1) " +
        "OR EXISTS (SELECT 1 FROM tblExternalTestConsultant WHERE fldExternalTestConsultantId = @TestConsultantId AND fldIsInactive = 1) " +
        "THEN 1 ELSE 0 END";

    public async Task<bool> IsInactiveAsync(Guid testConsultantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(IsInactiveSql, new { TestConsultantId = testConsultantId });
    }

    private static async Task ReadParticipantsAsync(SqlMapper.GridReader results, TestConsultantTabulationEntity entity)
    {
        var participants = new Dictionary<Guid, TabulationSourceParticipant>();
        foreach (var row in await results.ReadAsync<Rows.TestConsultantParticipant>())
        {
            var participant = new TabulationSourceParticipant
            {
                ParticipantId = row.ParticipantId,
                LabCode = row.LabCode ?? string.Empty,
                NilReturnReason = row.NilReturnReason ?? string.Empty,
            };
            participants[row.ParticipantId] = participant;
            entity.Source.Participants.Add(participant);
            entity.Participants.Add(new TestConsultantParticipantInformation(
                participant.LabCode, row.ExtraDocumentsReturned ?? false, row.DeadlineChangeReason ?? string.Empty));
        }

        foreach (var row in await results.ReadAsync<Rows.DraftRating>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.SampleRatings.TryAdd(row.SampleId, row.Rating ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.DraftParticipantMethod>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.MethodResults.TryAdd(row.TestMethodItemId, row.AmendedResult ?? string.Empty);
        }

        foreach (var row in await results.ReadAsync<Rows.DraftParticipantResult>())
        {
            participants.GetValueOrDefault(row.ParticipantId)?.ResultItemResults.TryAdd((row.SampleId, row.TestResultItemId), row.AmendedResult ?? string.Empty);
        }
    }
}
