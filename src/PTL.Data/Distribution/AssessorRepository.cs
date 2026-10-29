using Dapper;
using PTL.Core.Distribution;
using PTL.Data.Infrastructure;
using Rows = PTL.Data.Distribution.TabulationReportRows;

namespace PTL.Data.Distribution;

// Legacy Assessor MonthlyDistributionInfoCollection, YearInfoCollection and SampleInfoCollection. @ReferenceDate is left to its NULL
// default - the procedures then use today's date for their seven-year window (legacy only overrides it from the ResultsTestDate
// test-harness appSetting). Every list is scoped to the signed-in Assessor by the procedures themselves.
public sealed partial class AssessorRepository(IDbConnectionFactory connectionFactory) : IAssessorRepository
{
    private const string YearsSql = "EXEC dbo.spgYearByAssessorId @AssessorId = @AssessorId";
    private const string MonthsSql = "EXEC dbo.spgMonthlyDistributionInfoForAssessor @YearId = @YearId, @AssessorId = @AssessorId";
    private const string ItemsSql = "EXEC dbo.spgAssessmentItemByTestId @TestId = @TestId";

    // One row when the test exists: 1 when the user is one of the scheme's four Assessors, otherwise 0.
    private const string AccessSql = """
        SELECT CASE WHEN @AssessorId IN (s.fldAssessor1, s.fldAssessor2, s.fldAssessor3, s.fldAssessor4) THEN 1 ELSE 0 END
        FROM tblDistributionTest dt
        INNER JOIN tblMonthlyDistributionScheme mds ON dt.fldDistributionSchemeId = mds.fldMonthlyDistributionSchemeId
        INNER JOIN tblScheme s ON s.fldSchemeId = mds.fldSchemeId
        WHERE dt.fldTestId = @TestId
        """;

    public async Task<IReadOnlyList<int>> GetYearsAsync(Guid assessorId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var years = await connection.QueryAsync<Rows.AssessorYear>(YearsSql, new { AssessorId = assessorId });
        return [.. years.Select(y => y.YearId)];
    }

    public async Task<IReadOnlyList<AssessorMonthEntity>> GetMonthsAsync(Guid assessorId, int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        List<Rows.AssessorMonth> months;
        List<Rows.AssessorDistribution> distributions;
        List<Rows.AssessorTest> tests;
        using (var results = await connection.QueryMultipleAsync(MonthsSql, new { YearId = yearId, AssessorId = assessorId }))
        {
            months = (await results.ReadAsync<Rows.AssessorMonth>()).ToList();
            distributions = (await results.ReadAsync<Rows.AssessorDistribution>()).ToList();
            tests = (await results.ReadAsync<Rows.AssessorTest>()).ToList();
        }

        var distributionsByMonth = distributions.ToLookup(d => d.MonthlyDistributionId);
        var testsByScheme = tests.ToLookup(t => t.MonthlyDistributionSchemeId);

        return [.. months.Select(month =>
        {
            var entity = new AssessorMonthEntity { YearId = month.YearId, MonthId = month.MonthId };
            foreach (var distribution in distributionsByMonth[month.MonthlyDistributionId])
            {
                var distributionEntity = new AssessorDistributionEntity
                {
                    MonthlyDistributionSchemeId = distribution.MonthlyDistributionSchemeId,
                    SchemeName = distribution.SchemeName ?? string.Empty,
                    SchemeIdentifier = distribution.SchemeIdentifier ?? string.Empty,
                    DistributionReference = distribution.DistributionReference ?? string.Empty,
                    DistributionReferenceSuffix = distribution.DistributionReferenceSuffix ?? string.Empty,
                    ScheduleCode = distribution.ScheduleCode ?? string.Empty,
                };
                distributionEntity.Tests.AddRange(testsByScheme[distribution.MonthlyDistributionSchemeId]
                    .Select(t => new AssessorTestEntity(t.TestId, t.TestType ?? string.Empty, t.AssessmentSubmitted)));
                entity.Distributions.Add(distributionEntity);
            }

            return entity;
        })];
    }

    public async Task<AssessorAccess> GetTestAccessAsync(Guid testId, Guid assessorId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var isAssessor = await connection.ExecuteScalarAsync<int?>(AccessSql, new { TestId = testId, AssessorId = assessorId });

        return isAssessor switch
        {
            null => AssessorAccess.NotFound,
            0 => AssessorAccess.Forbidden,
            _ => AssessorAccess.Granted,
        };
    }

    public async Task<AssessorItemListEntity?> GetItemListAsync(Guid testId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        Rows.AssessorItemHeader? header;
        List<Rows.AssessorItemSample> samples;
        List<Rows.AssessorItem> items;
        using (var results = await connection.QueryMultipleAsync(ItemsSql, new { TestId = testId }))
        {
            header = await results.ReadFirstOrDefaultAsync<Rows.AssessorItemHeader>();
            samples = (await results.ReadAsync<Rows.AssessorItemSample>()).ToList();
            items = (await results.ReadAsync<Rows.AssessorItem>()).ToList();
        }

        if (header is null)
        {
            return null;
        }

        var list = new AssessorItemListEntity
        {
            YearId = header.YearId,
            MonthId = header.MonthId,
            SchemeName = header.SchemeName ?? string.Empty,
            SchemeIdentifier = header.SchemeIdentifier ?? string.Empty,
            DistributionReference = header.DistributionReference ?? string.Empty,
            DistributionReferenceSuffix = header.DistributionReferenceSuffix ?? string.Empty,
            ScheduleCode = header.ScheduleCode ?? string.Empty,
            TestName = header.TestName ?? string.Empty,
            AssessmentSubmitted = header.AssessmentSubmitted,
            AssessorName = header.AssessorName ?? string.Empty,
        };

        var itemsBySample = items.ToLookup(i => i.SampleId);
        foreach (var sample in samples)
        {
            var sampleEntity = new AssessorSampleEntity { SampleId = sample.SampleId, SampleNumber = sample.SampleNumber ?? string.Empty };
            sampleEntity.Items.AddRange(itemsBySample[sample.SampleId]
                .Select(i => new AssessorItemEntity(i.SampleTestId, i.DistributionParticipantId, i.Labcode ?? string.Empty, i.AssessmentDate)));
            list.Samples.Add(sampleEntity);
        }

        return list;
    }
}
