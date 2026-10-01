using System.Data;
using PTL.Data.Infrastructure;
using PTL.Data.PostagePricingPlan;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.PostagePricingPlan;

// Exercises PostagePricingPlanRepository against a fake ADO.NET connection (see Fakes/) instead
// of a live SQL Server - see WeightedPricingPlanRepositoryTests for the pattern this follows.
public class PostagePricingPlanRepositoryTests
{
    public PostagePricingPlanRepositoryTests() => DapperColumnMappings.Register();

    private const string SetPriceSql = "EXEC dbo.spuPostage @fldPostageId, @fldUkPrice, @fldEuPrice, @fldNonEuPrice, @fldYearId";
    private const string RenewSql = "EXEC dbo.sppRenewPostagePricingPlan @YearId";
    private const string GetByIdSql = "EXEC dbo.spgPostageByPostageID @PostageId";

    private static (PostagePricingPlanRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new PostagePricingPlanRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetYearsWithPlanAsync_ReturnsMappedYears()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldYear", typeof(string));
        table.Rows.Add(2026, "2026/27");
        connection.RespondToQuery("EXEC dbo.spgaPostagePricingYears", table);

        var result = await repository.GetYearsWithPlanAsync();

        Assert.Single(result);
        Assert.Equal("2026/27", result[0].Year);
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedRow()
    {
        var (repository, connection) = CreateRepository();
        var postageId = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldPostageId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldUkPrice", typeof(decimal));
        table.Columns.Add("fldEuPrice", typeof(decimal));
        table.Columns.Add("fldNonEuPrice", typeof(decimal));
        table.Columns.Add("fldYearId", typeof(int));
        table.Rows.Add(postageId, "Courier", 5.00m, 10.00m, 15.00m, 2026);
        connection.RespondToQuery(GetByIdSql, table);

        var result = await repository.GetByIdAsync(postageId);

        Assert.NotNull(result);
        Assert.Equal("Courier", result!.Name);
        Assert.Equal(2026, result.YearId);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetByIdSql, new DataTable());

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SetPriceAsync_ExecutesUpdateStoredProcedureWithParameters()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(SetPriceSql, 1);
        var postageId = Guid.NewGuid();

        await repository.SetPriceAsync(postageId, 2026, 5.00m, 10.00m, 15.00m);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == SetPriceSql);
        Assert.Equal(postageId, command.ParameterValue("@fldPostageId"));
        Assert.Equal(5.00m, command.ParameterValue("@fldUkPrice"));
        Assert.Equal(10.00m, command.ParameterValue("@fldEuPrice"));
        Assert.Equal(15.00m, command.ParameterValue("@fldNonEuPrice"));
        Assert.Equal(2026, command.ParameterValue("@fldYearId"));
    }

    [Fact]
    public async Task RenewAsync_ExecutesRenewStoredProcedureWithNewYearId()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(RenewSql, 1);

        await repository.RenewAsync(2027);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == RenewSql);
        Assert.Equal(2027, command.ParameterValue("@YearId"));
    }
}
