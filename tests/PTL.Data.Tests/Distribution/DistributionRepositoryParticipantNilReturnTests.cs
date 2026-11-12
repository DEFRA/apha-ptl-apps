using System.Data;
using PTL.Core.Distribution;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

public class DistributionRepositoryParticipantNilReturnTests
{
    public DistributionRepositoryParticipantNilReturnTests() => DapperColumnMappings.Register();

    private static readonly Guid DistributionParticipantId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid SchemeId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    private const string GetNilReturnSql = """
            SELECT
                dp.fldDistributionParticipantId AS DistributionParticipantId,
                dp.fldNilReturnReason AS NilReturnReason
            FROM dbo.tlnkDistributionParticipant dp
            WHERE dp.fldDistributionParticipantId = @DistributionParticipantId
            """;

    private const string SaveNilReturnSql =
        "EXEC dbo.spuDistributionParticipantForAmendResults @DistributionParticipantId = @DistributionParticipantId, @NilReturnReason = @NilReturnReason";

    private const string GetSampleReceiptSql =
        "EXEC dbo.spgSampleReceipt @DistributionParticipantId = @DistributionParticipantId, @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static DataTable NilReturnTable()
    {
        var table = new DataTable();
        table.Columns.Add("DistributionParticipantId", typeof(Guid));
        table.Columns.Add("NilReturnReason", typeof(string));
        return table;
    }

    private static DataTable TestTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldTestId", typeof(Guid));
        table.Columns.Add("fldTestType", typeof(string));
        return table;
    }

    private static DataTable SampleTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldSampleReceiptId", typeof(Guid));
        table.Columns.Add("fldTestId", typeof(Guid));
        table.Columns.Add("fldSampleNumber", typeof(string));
        table.Columns.Add("fldSubmissionDate", typeof(DateTime));
        table.Columns.Add("fldNonSubmissionDate", typeof(DateTime));
        table.Columns.Add("fldNonCapableDate", typeof(DateTime));
        return table;
    }

    private static DataTable LabTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldLabCode", typeof(string));
        table.Columns.Add("fldLabName", typeof(string));
        return table;
    }

    private static DataTable SchemeTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldDistributionReference", typeof(string));
        table.Columns.Add("fldDistributionReferenceSuffix", typeof(string));
        table.Columns.Add("fldScheduleCode", typeof(string));
        table.Columns.Add("fldIsReadyForAssessor", typeof(DateTime));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldIdentifier", typeof(string));
        return table;
    }

    [Fact]
    public async Task GetParticipantNilReturnAsync_MapsReason()
    {
        var (repository, connection) = CreateRepository();
        var rows = NilReturnTable();
        rows.Rows.Add(DistributionParticipantId, "No samples");
        connection.RespondToQuery(GetNilReturnSql, rows);

        var result = await repository.GetParticipantNilReturnAsync(DistributionParticipantId);

        Assert.NotNull(result);
        Assert.Equal(DistributionParticipantId, result!.DistributionParticipantId);
        Assert.Equal("No samples", result.NilReturnReason);
        Assert.Equal(DistributionParticipantId, Assert.Single(connection.ExecutedCommands).ParameterValue("DistributionParticipantId"));
    }

    [Fact]
    public async Task SaveParticipantNilReturnReasonAsync_ExecutesProcedureWithParameters()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(SaveNilReturnSql, 1);

        await repository.SaveParticipantNilReturnReasonAsync(DistributionParticipantId, "Reason text");

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(SaveNilReturnSql, command.CommandText);
        Assert.Equal(DistributionParticipantId, command.ParameterValue("DistributionParticipantId"));
        Assert.Equal("Reason text", command.ParameterValue("NilReturnReason"));
    }

    [Fact]
    public async Task GetSampleReceiptRowsForParticipantAsync_ReturnsRowsFromSampleReceipt()
    {
        var (repository, connection) = CreateRepository();

        var tests = TestTable();
        var testId = Guid.NewGuid();
        tests.Rows.Add(testId, "Culture");

        var samples = SampleTable();
        var one = Guid.NewGuid();
        var two = Guid.NewGuid();
        samples.Rows.Add(one, testId, "S001", DBNull.Value, DBNull.Value, DBNull.Value);
        samples.Rows.Add(two, testId, "S002", DBNull.Value, DBNull.Value, DBNull.Value);

        var lab = LabTable();
        lab.Rows.Add("L001", "Lab One");

        var scheme = SchemeTable();
        scheme.Rows.Add("D1", "a", "S1", DBNull.Value, "Brucella", "BT");

        var data = new DataSet();
        data.Tables.Add(tests);
        data.Tables.Add(samples);
        data.Tables.Add(lab);
        data.Tables.Add(scheme);
        connection.RespondToQuery(GetSampleReceiptSql, data);

        var rows = await repository.GetSampleReceiptRowsForParticipantAsync(DistributionParticipantId, SchemeId);

        Assert.Equal(2, rows.Count);
        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(GetSampleReceiptSql, command.CommandText);
        Assert.Equal(DistributionParticipantId, command.ParameterValue("DistributionParticipantId"));
        Assert.Equal(SchemeId, command.ParameterValue("MonthlyDistributionSchemeId"));
    }
}
