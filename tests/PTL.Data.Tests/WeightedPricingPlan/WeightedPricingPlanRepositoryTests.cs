using System.Data;
using PTL.Core.Audit;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;
using PTL.Data.WeightedPricingPlan;

namespace PTL.Data.Tests.WeightedPricingPlan;

// Exercises WeightedPricingPlanRepository against a fake ADO.NET connection (see Fakes/) instead
// of a live SQL Server - see ContractRepositoryTests/LookupRepositoryTests for the pattern this follows.
public class WeightedPricingPlanRepositoryTests
{
    public WeightedPricingPlanRepositoryTests() => DapperColumnMappings.Register();

    private const string RenewSql = "EXEC dbo.sppRenewPricingPlan @YearId";

    private static (WeightedPricingPlanRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new WeightedPricingPlanRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetYearsWithPercentagesAsync_ReturnsMappedYears()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldYear", typeof(string));
        table.Rows.Add(2026, "2026/27");
        connection.RespondToQuery("EXEC dbo.spgaWeightedPricingYear", table);

        var result = await repository.GetYearsWithPercentagesAsync();

        Assert.Single(result);
        Assert.Equal("2026/27", result[0].Year);
    }

    [Fact]
    public async Task GetPercentagesForYearAsync_ReturnsMappedPercentages()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldPricingPercentageId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldNumberOfDistributionsOnScheme", typeof(int));
        table.Columns.Add("fldNumberOfDistributionsChosen", typeof(int));
        table.Columns.Add("fldWeight", typeof(int));
        table.Rows.Add(Guid.NewGuid(), 2026, 12, 6, 50);
        connection.RespondToQuery("EXEC dbo.spgPricingPercentageByYearId @YearId", table);

        var result = await repository.GetPercentagesForYearAsync(2026);

        Assert.Single(result);
        Assert.Equal(50, result[0].Weight);
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

    private const string AuditSql = "EXEC dbo.spiSystemAdministrationAudit @AuditId, @ActingUserId, @Action, @EntityType, @EntityId, @Details";

    [Fact]
    public async Task RenewAsync_WithAudit_CommitsChangeAndAuditInOneTransaction()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(RenewSql, 1);
        connection.RespondToNonQuery(AuditSql, 1);
        var audit = new AuditEntry(Guid.NewGuid(), "RenewWeightedPricingPlan", "WeightedPricingPlan", "2027", "Renewed");

        await repository.RenewAsync(2027, audit);

        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.Committed);
        Assert.Contains(connection.ExecutedCommands, c => c.CommandText == RenewSql);
        var auditCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AuditSql);
        Assert.Equal("RenewWeightedPricingPlan", auditCommand.ParameterValue("@Action"));
    }

    [Fact]
    public async Task RenewAsync_AuditWriteFails_RollsBackTheTransaction()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(RenewSql, 1);
        var audit = new AuditEntry(Guid.NewGuid(), "RenewWeightedPricingPlan", "WeightedPricingPlan", "2027", "Renewed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RenewAsync(2027, audit));

        Assert.NotNull(connection.LastTransaction);
        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction!.Committed);
    }
}
