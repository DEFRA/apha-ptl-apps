using System.Data;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// ExternalWeb View reports - spgTabulations' @SsoIdExt branch (PTL.Database script 0017).
public class DistributionRepositoryParticipantTabulationsTests
{
    private const string Sql = "EXEC dbo.spgTabulations @SsoIdExt = @SsoIdExt, @ParticipantId = @ParticipantId";

    private static readonly Guid SsoIdExt = Guid.Parse("6a8754e2-2ce7-f111-84e7-70a8a53681e7");
    private static readonly Guid LabId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TabulationId = Guid.Parse("22222222-0000-0000-0000-000000000001");

    public DistributionRepositoryParticipantTabulationsTests() => DapperColumnMappings.Register();

    private static DataTable Rows(params object[][] rows)
    {
        var table = new DataTable();
        table.Columns.Add("fldTabulationId", typeof(Guid));
        table.Columns.Add("fldDistributionReference", typeof(string));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldMonthId", typeof(int));
        table.Columns.Add("fldSchemeName", typeof(string));
        table.Columns.Add("fldTabulationName", typeof(string));
        table.Columns.Add("fldDistributionDate", typeof(DateTime));
        foreach (var row in rows)
        {
            table.Rows.Add(row);
        }

        return table;
    }

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository(DataTable table)
    {
        var connection = new FakeDbConnection();
        connection.RespondToQuery(Sql, table);
        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    [Fact]
    public async Task GetTabulationsForExternalUserAsync_BindsContactIdAndLabByNameAndMapsRows()
    {
        var (repository, connection) = CreateRepository(Rows(
            [TabulationId, "D1a/S1", 2026, 5, "BT: Brucella", "Summary", new DateTime(2026, 5, 1)]));

        var rows = await repository.GetTabulationsForExternalUserAsync(SsoIdExt, LabId);

        var row = Assert.Single(rows);
        Assert.Equal((TabulationId, "D1a/S1", 2026, 5), (row.TabulationId, row.DistributionReference, row.YearId, row.MonthId));
        Assert.Equal(("BT: Brucella", "BT", "Brucella", "Summary"), (row.SchemeName, row.SchemeCode, row.SchemeNameOnly, row.TabulationName));
        Assert.Equal(new DateTime(2026, 5, 1), row.DistributionDate);

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(Sql, command.CommandText);
        Assert.Equal(SsoIdExt, command.ParameterValue("SsoIdExt"));
        Assert.Equal(LabId, command.ParameterValue("ParticipantId"));
    }

    [Fact]
    public async Task GetTabulationsForExternalUserAsync_NoLab_SendsNull()
    {
        var (repository, connection) = CreateRepository(Rows());

        var rows = await repository.GetTabulationsForExternalUserAsync(SsoIdExt, null);

        Assert.Empty(rows);
        Assert.True(Assert.Single(connection.ExecutedCommands).ParameterValue("ParticipantId") is null or DBNull);
    }
}
