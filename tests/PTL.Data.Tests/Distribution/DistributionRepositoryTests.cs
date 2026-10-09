using System.Data;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

public class DistributionRepositoryTests
{
    public DistributionRepositoryTests() => DapperColumnMappings.Register();

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new DistributionRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetMonthlyDistributionSummariesAsync_ReturnsMappedSummaries()
    {
        var (repository, connection) = CreateRepository();
        var monthlyDistributionId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionId", typeof(Guid));
        table.Columns.Add("fldMonthId", typeof(int));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldNumberOfSchemes", typeof(int));
        table.Columns.Add("fldNumberOfSampleNumbersDefined", typeof(int));
        table.Columns.Add("fldNumberOfPrepComplete", typeof(int));
        table.Columns.Add("fldNumberOfParticipants", typeof(int));
        table.Columns.Add("fldNumberOfPackagingComplete", typeof(int));
        table.Columns.Add("fldNumberOfResultsEntered", typeof(int));
        table.Columns.Add("fldNumberOfTabulations", typeof(int));
        table.Columns.Add("fldNumberOfCompleteTabulations", typeof(int));
        table.Rows.Add(monthlyDistributionId, 4, 2026, 3, 2, 1, 10, 5, 4, 2, 1);
        connection.RespondToQuery("EXEC dbo.spgaMonthlyDistributionInfo", table);

        var result = await repository.GetMonthlyDistributionSummariesAsync();

        Assert.Single(result);
        Assert.Equal(monthlyDistributionId, result[0].MonthlyDistributionId);
        Assert.Equal(4, result[0].MonthId);
        Assert.Equal(2026, result[0].YearId);
        Assert.Equal(3, result[0].NumberOfSchemes);
        Assert.Equal(10, result[0].NumberOfParticipants);
    }

    [Fact]
    public async Task GetDistributionYearsAsync_ReturnsMappedYears()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldMonthId", typeof(int));
        table.Rows.Add(2026, 6);
        connection.RespondToQuery("EXEC dbo.spgaDistributionYearInfo", table);

        var result = await repository.GetDistributionYearsAsync();

        Assert.Single(result);
        Assert.Equal(2026, result[0].YearId);
        Assert.Equal(6, result[0].MonthId);
    }

    [Fact]
    public async Task GetMonthlyDistributionSchedulesAsync_MonthNotInitialised_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        var header = new DataTable();
        header.Columns.Add("fldMonthlyDistributionId", typeof(Guid));
        header.Columns.Add("fldYearId", typeof(int));
        header.Columns.Add("fldMonthId", typeof(int));
        var schemes = new DataTable();
        schemes.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        var participants = new DataTable();
        participants.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        participants.Columns.Add("fldNumberOfSetsRequired", typeof(int));
        var dataSet = new DataSet();
        dataSet.Tables.Add(header);
        dataSet.Tables.Add(schemes);
        dataSet.Tables.Add(participants);
        connection.RespondToQuery("EXEC dbo.spgMonthlyDistribution @YearId = @YearId, @MonthId = @MonthId", dataSet);

        var result = await repository.GetMonthlyDistributionSchedulesAsync(2026, 4);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMonthlyDistributionSchedulesAsync_MonthInitialised_AggregatesParticipantsOntoSchemes()
    {
        var (repository, connection) = CreateRepository();
        var monthlyDistributionId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();

        var header = new DataTable();
        header.Columns.Add("fldMonthlyDistributionId", typeof(Guid));
        header.Columns.Add("fldYearId", typeof(int));
        header.Columns.Add("fldMonthId", typeof(int));
        header.Rows.Add(monthlyDistributionId, 2026, 4);

        var schemes = new DataTable();
        schemes.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        schemes.Columns.Add("fldMonthlyDistributionId", typeof(Guid));
        schemes.Columns.Add("fldSchemeId", typeof(Guid));
        schemes.Columns.Add("SchemeIdentifier", typeof(string));
        schemes.Columns.Add("SchemeName", typeof(string));
        schemes.Columns.Add("fldDistributionReference", typeof(string));
        schemes.Columns.Add("fldDistributionReferenceSuffix", typeof(string));
        schemes.Columns.Add("fldScheduleCode", typeof(string));
        schemes.Columns.Add("fldDistributionDate", typeof(DateTime));
        schemes.Columns.Add("fldOverseasPostingDate", typeof(DateTime));
        schemes.Columns.Add("fldDeadlineDate", typeof(DateTime));
        schemes.Columns.Add("fldResultsIssueTargetDate", typeof(DateTime));
        schemes.Columns.Add("fldComments", typeof(string));
        schemes.Columns.Add("fldHasIntendedResults", typeof(bool));
        schemes.Columns.Add("fldHasSampleNumbersDefined", typeof(bool));
        schemes.Columns.Add("fldIsCancelled", typeof(bool));
        schemes.Columns.Add("fldIsAsAvailable", typeof(bool));
        schemes.Columns.Add("fldStoreRatings", typeof(bool));
        schemes.Columns.Add("fldSchemeVersionDate", typeof(DateTime));
        schemes.Rows.Add(
            schemeId, monthlyDistributionId, Guid.NewGuid(), "S001", "Test Scheme", "D26-01", "A", "BA",
            new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), new DateTime(2026, 4, 5), new DateTime(2026, 4, 10),
            "Some comments", false, false, false, false, false, new DateTime(2026, 1, 1));

        var participants = new DataTable();
        participants.Columns.Add("fldDistributionSchemeId", typeof(Guid));
        participants.Columns.Add("fldNumberOfSetsRequired", typeof(int));
        participants.Rows.Add(schemeId, 3);
        participants.Rows.Add(schemeId, 2);

        var dataSet = new DataSet();
        dataSet.Tables.Add(header);
        dataSet.Tables.Add(schemes);
        dataSet.Tables.Add(participants);
        connection.RespondToQuery("EXEC dbo.spgMonthlyDistribution @YearId = @YearId, @MonthId = @MonthId", dataSet);

        var result = await repository.GetMonthlyDistributionSchedulesAsync(2026, 4);

        Assert.NotNull(result);
        Assert.Equal(monthlyDistributionId, result!.Value.MonthlyDistributionId);
        var scheme = Assert.Single(result.Value.Schemes);
        Assert.Equal(2, scheme.ParticipantCount);
        Assert.Equal(5, scheme.TotalSetsOfSamplesRequired);
        Assert.Equal("D26-01A/BA", scheme.DistributionReferenceFull);
    }

    [Fact]
    public async Task UpdateMonthlyDistributionSchemesAsync_ExecutesOneCommandPerSchemeAndCommits()
    {
        var (repository, connection) = CreateRepository();
        const string sql = """
            EXEC dbo.spuMonthlyDistributionScheme
                @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
                @MonthlyDistributionId = @MonthlyDistributionId,
                @SchemeId = @SchemeId,
                @MonthlyDistributionReference = @DistributionReference,
                @MonthlyDistributionReferenceSuffix = @DistributionReferenceSuffix,
                @MonthlyDistributionDate = @DistributionDate,
                @DeadlineDate = @DeadlineDate,
                @OverseasPostingDate = @OverseasPostingDate,
                @ResultsIssueTargetDate = @ResultsIssueTargetDate,
                @Comments = @Comments,
                @HasIntendedResults = @HasIntendedResults,
                @HasSampleNumbersDefined = @HasSampleNumbersDefined,
                @IsCancelled = @IsCancelled,
                @StoreRatings = @StoreRatings,
                @SchemeVersionDate = @SchemeVersionDate
            """;
        connection.RespondToNonQuery(sql, 1);

        var schemes = new List<PTL.Core.Distribution.MonthlyDistributionSchemeEntity>
        {
            new() { MonthlyDistributionSchemeId = Guid.NewGuid() },
            new() { MonthlyDistributionSchemeId = Guid.NewGuid() },
        };

        await repository.UpdateMonthlyDistributionSchemesAsync(schemes);

        Assert.Equal(2, connection.ExecutedCommands.Count(c => c.CommandText == sql));
        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.Committed);
    }
}
