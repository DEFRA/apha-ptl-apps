using System.Data;
using PTL.Core.Distribution;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// Sample Receipt persistence - legacy ResultsReceipt.TestCollection.DataPortal_Fetch / DataPortal_Update.
public class DistributionRepositorySampleReceiptTests
{
    public DistributionRepositorySampleReceiptTests() => DapperColumnMappings.Register();

    private static readonly Guid ParticipantId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid SchemeId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    private const string GetSampleReceiptSql =
        "EXEC dbo.spgSampleReceipt @DistributionParticipantId = @DistributionParticipantId, @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";

    private const string UpdateSampleReceiptSql = """
        EXEC dbo.spuSampleReceipt
            @SampleReceiptId = @SampleReceiptId,
            @SubmissionDate = @SubmissionDate,
            @NonSubmissionDate = @NonSubmissionDate,
            @NonCapableDate = @NonCapableDate
        """;

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
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

    private static DataSet DataSetOf(DataTable tests, DataTable samples, DataTable lab, DataTable scheme)
    {
        var dataSet = new DataSet();
        dataSet.Tables.Add(tests);
        dataSet.Tables.Add(samples);
        dataSet.Tables.Add(lab);
        dataSet.Tables.Add(scheme);
        return dataSet;
    }

    [Fact]
    public async Task GetSampleReceiptAsync_LabNotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        var scheme = SchemeTable();
        scheme.Rows.Add("D1", "a", "S1", DBNull.Value, "Brucella", "BT");
        connection.RespondToQuery(GetSampleReceiptSql, DataSetOf(TestTable(), SampleTable(), LabTable(), scheme));

        Assert.Null(await repository.GetSampleReceiptAsync(ParticipantId, SchemeId));
    }

    [Fact]
    public async Task GetSampleReceiptAsync_SchemeNotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        var lab = LabTable();
        lab.Rows.Add("L001", "Lab One");
        connection.RespondToQuery(GetSampleReceiptSql, DataSetOf(TestTable(), SampleTable(), lab, SchemeTable()));

        Assert.Null(await repository.GetSampleReceiptAsync(ParticipantId, SchemeId));
    }

    [Fact]
    public async Task GetSampleReceiptAsync_MapsAllFourResultSets()
    {
        var (repository, connection) = CreateRepository();
        var testId = Guid.NewGuid();
        var submitted = Guid.NewGuid();
        var blank = Guid.NewGuid();

        var tests = TestTable();
        tests.Rows.Add(testId, "Culture");

        var samples = SampleTable();
        samples.Rows.Add(submitted, testId, "S001", new DateTime(2026, 5, 1), DBNull.Value, DBNull.Value);
        samples.Rows.Add(blank, testId, "S002", DBNull.Value, DBNull.Value, DBNull.Value);

        var lab = LabTable();
        lab.Rows.Add("L001", "Lab One");

        var scheme = SchemeTable();
        scheme.Rows.Add("D1", "a", "S1", new DateTime(2026, 5, 20), "Brucella", "BT");

        connection.RespondToQuery(GetSampleReceiptSql, DataSetOf(tests, samples, lab, scheme));

        var result = await repository.GetSampleReceiptAsync(ParticipantId, SchemeId);

        Assert.NotNull(result);
        var query = connection.ExecutedCommands.Single(c => c.CommandText == GetSampleReceiptSql);
        Assert.Equal(ParticipantId, query.ParameterValue("DistributionParticipantId"));
        Assert.Equal(SchemeId, query.ParameterValue("MonthlyDistributionSchemeId"));

        Assert.Equal(("D1a/S1", new DateTime(2026, 5, 20), "Brucella", "BT"),
            (result!.Scheme.DistributionReferenceFull, result.Scheme.IsReadyForAssessor, result.Scheme.SchemeName, result.Scheme.SchemeIdentifier));
        Assert.Equal(("L001", "Lab One"), (result.Lab.LabCode, result.Lab.LabName));
        Assert.Equal((testId, "Culture"), (result.Tests.Single().TestId, result.Tests.Single().TestType));

        var first = result.Samples.Single(s => s.SampleReceiptId == submitted);
        Assert.Equal((testId, "S001", new DateTime(2026, 5, 1)), (first.TestId, first.SampleNumber, first.SubmissionDate));
        Assert.Null(first.NonSubmissionDate);
        Assert.Null(first.NonCapableDate);

        var second = result.Samples.Single(s => s.SampleReceiptId == blank);
        Assert.Null(second.SubmissionDate);
    }

    [Fact]
    public async Task GetSampleReceiptAsync_SchemeNotReadyForAssessor_LeavesTheDateNull()
    {
        var (repository, connection) = CreateRepository();
        var lab = LabTable();
        lab.Rows.Add("L001", "Lab One");
        var scheme = SchemeTable();
        scheme.Rows.Add("D1", "", "S1", DBNull.Value, "Brucella", "BT");
        connection.RespondToQuery(GetSampleReceiptSql, DataSetOf(TestTable(), SampleTable(), lab, scheme));

        var result = await repository.GetSampleReceiptAsync(ParticipantId, SchemeId);

        Assert.Null(result!.Scheme.IsReadyForAssessor);
    }

    [Fact]
    public async Task UpdateSampleReceiptAsync_NoSamples_DoesNotOpenATransaction()
    {
        var (repository, connection) = CreateRepository();

        await repository.UpdateSampleReceiptAsync([]);

        Assert.Empty(connection.ExecutedCommands);
        Assert.Null(connection.LastTransaction);
    }

    [Fact]
    public async Task UpdateSampleReceiptAsync_ExecutesEachSampleInOneTransactionAndCommits()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateSampleReceiptSql, 1);
        var one = new SampleReceiptSampleEntity { SampleReceiptId = Guid.NewGuid(), SubmissionDate = new DateTime(2026, 5, 12) };
        var two = new SampleReceiptSampleEntity { SampleReceiptId = Guid.NewGuid(), NonCapableDate = new DateTime(2026, 5, 12) };

        await repository.UpdateSampleReceiptAsync([one, two]);

        var commands = connection.ExecutedCommands.Where(c => c.CommandText == UpdateSampleReceiptSql).ToList();
        Assert.Equal(2, commands.Count);
        Assert.Equal(one.SampleReceiptId, commands[0].ParameterValue("SampleReceiptId"));
        Assert.Equal(new DateTime(2026, 5, 12), commands[0].ParameterValue("SubmissionDate"));
        Assert.Equal(DBNull.Value, commands[0].ParameterValue("NonSubmissionDate"));
        Assert.Equal(two.SampleReceiptId, commands[1].ParameterValue("SampleReceiptId"));
        Assert.Equal(new DateTime(2026, 5, 12), commands[1].ParameterValue("NonCapableDate"));
        Assert.True(connection.LastTransaction!.Committed);
    }
}
