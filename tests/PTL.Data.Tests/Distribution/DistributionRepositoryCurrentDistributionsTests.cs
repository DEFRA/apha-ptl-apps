using System.Data;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// External Enter Results -> Current Distributions (legacy spgCurrentDistributionsBySsoId, adapted to a participant id).
public class DistributionRepositoryCurrentDistributionsTests
{
    public DistributionRepositoryCurrentDistributionsTests() => DapperColumnMappings.Register();

    private static readonly Guid ParticipantId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid SchemeId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid DistributionParticipantId = Guid.Parse("33333333-0000-0000-0000-000000000001");

    private static DataTable Rows(params object[][] rows)
    {
        var table = new DataTable();
        table.Columns.Add("MonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("DistributionParticipantId", typeof(Guid));
        table.Columns.Add("DistributionReference", typeof(string));
        table.Columns.Add("SchemeCode", typeof(string));
        table.Columns.Add("SchemeName", typeof(string));
        table.Columns.Add("DistributionDate", typeof(DateTime));
        table.Columns.Add("DeadlineDate", typeof(DateTime));
        table.Columns.Add("HasResults", typeof(bool));
        table.Columns.Add("Submitted", typeof(bool));
        foreach (var row in rows)
        {
            table.Rows.Add(row);
        }

        return table;
    }

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository(DataTable table)
    {
        var connection = new FakeDbConnection();
        connection.RespondToQueryContaining("FROM dbo.tblMonthlyDistributionScheme mds", table);
        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    [Fact]
    public async Task GetCurrentDistributionsAsync_MapsEachRowAndAsksForTheParticipant()
    {
        var (repository, connection) = CreateRepository(Rows(
            [SchemeId, DistributionParticipantId, "D1a/00001", "PT1", "Bacteriology", new DateTime(2026, 1, 5), new DateTime(2026, 1, 30), true, false]));

        var rows = await repository.GetCurrentDistributionsAsync(ParticipantId);

        var row = Assert.Single(rows);
        Assert.Equal((SchemeId, DistributionParticipantId, "D1a/00001", "PT1", "Bacteriology"),
            (row.MonthlyDistributionSchemeId, row.DistributionParticipantId, row.DistributionReference, row.SchemeCode, row.SchemeName));
        Assert.Equal((new DateTime(2026, 1, 5), new DateTime(2026, 1, 30), true, false),
            (row.DistributionDate, row.DeadlineDate, row.HasResults, row.Submitted));
        Assert.Equal(ParticipantId, Assert.Single(connection.ExecutedCommands).ParameterValue("ParticipantId"));
    }

    [Fact]
    public async Task GetCurrentDistributionsAsync_NoRows_ReturnsAnEmptyList() =>
        Assert.Empty(await CreateRepository(Rows()).Repository.GetCurrentDistributionsAsync(ParticipantId));

    [Fact]
    public async Task GetCurrentDistributionsAsync_KeepsLegacyOpenRulesAndOrder()
    {
        var (repository, connection) = CreateRepository(Rows());

        await repository.GetCurrentDistributionsAsync(ParticipantId);

        var sql = Assert.Single(connection.ExecutedCommands).CommandText;
        Assert.Contains("dp.fldPostedDate IS NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("mds.fldIsCancelled = 0", sql, StringComparison.Ordinal);
        Assert.Contains("ISNULL(dp.fldDeadlineDate, mds.fldDeadlineDate) >=", sql, StringComparison.Ordinal);
        Assert.Contains("fnGetSevenYearsAgo", sql, StringComparison.Ordinal);
        Assert.Contains("mds.fldDistributionReference,", sql, StringComparison.Ordinal);
        Assert.Contains("mds.fldDistributionReferenceSuffix", sql, StringComparison.Ordinal);
    }
}
