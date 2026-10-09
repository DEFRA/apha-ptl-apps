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
}
