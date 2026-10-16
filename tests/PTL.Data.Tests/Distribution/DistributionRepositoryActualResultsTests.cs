using System.Data;
using PTL.Core.Distribution;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// Actual Results persistence - legacy ActualResultsSet.DataPortal_Fetch / DataPortal_Update.
public class DistributionRepositoryActualResultsTests
{
    public DistributionRepositoryActualResultsTests() => DapperColumnMappings.Register();

    private static readonly Guid ParticipantId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly DateTime First = new(2026, 6, 1, 9, 0, 0);
    private static readonly DateTime Last = new(2026, 6, 15, 12, 0, 0);

    private const string GetActualResultsSql = "EXEC dbo.spgActualResults @ActualResultsId = @ActualResultsId";
    private const string GetRatingIdsSql = "EXEC dbo.spgaRating";

    private const string InsertActualResultsSql =
        "EXEC dbo.spiActualResults @ActualResultsId = @ActualResultsId, @FirstEditDate = @FirstEditDate, @LastEditDate = @LastEditDate";

    private const string UpdateActualResultsSql =
        "EXEC dbo.spuActualResults @ActualResultsId = @ActualResultsId, @FirstEditDate = @FirstEditDate, @LastEditDate = @LastEditDate";

    private const string InsertMethodItemSql =
        "EXEC dbo.spiActualMethodItemResult @ActualResultsId = @ActualResultsId, @TestMethodItemId = @TestMethodItemId, @Result = @Result";

    private const string AuditMethodItemSql = """
        EXEC dbo.spiAuditActualTestMethodItem
            @ActualResultsId = @ActualResultsId,
            @TestMethodItemId = @TestMethodItemId,
            @AuditWho = @AuditWho,
            @AuditWhy = @AuditWhy,
            @Result = @Result,
            @ResultUpdate = 1
        """;

    private const string UpdateMethodItemSql =
        "EXEC dbo.spuActualMethodItemResult @ActualResultsId = @ActualResultsId, @TestMethodItemId = @TestMethodItemId, @Result = @Result";

    private const string InsertSampleItemSql =
        "EXEC dbo.spiActualResultItemResult @ActualResultsId = @ActualResultsId, @SampleResultItemId = @SampleResultItemId, @Result = @Result";

    private const string AuditSampleItemSql = """
        EXEC dbo.spiAuditActualSampleResultItem
            @ActualResultsId = @ActualResultsId,
            @SampleResultItemId = @SampleResultItemId,
            @AuditWho = @AuditWho,
            @AuditWhy = @AuditWhy,
            @Result = @Result,
            @ResultUpdate = 1
        """;

    private const string UpdateSampleItemSql =
        "EXEC dbo.spuActualResultItemResult @ActualResultsId = @ActualResultsId, @SampleResultItemId = @SampleResultItemId, @Result = @Result";

    private const string InsertSampleRatingSql =
        "EXEC dbo.spiSampleRating @SampleId = @SampleId, @ParticipantId = @ParticipantId, @RatingId = @RatingId";

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static FakeDbConnection RespondToAllWrites(FakeDbConnection connection)
    {
        foreach (var sql in new[]
        {
            InsertActualResultsSql, UpdateActualResultsSql, InsertMethodItemSql, AuditMethodItemSql, UpdateMethodItemSql,
            InsertSampleItemSql, AuditSampleItemSql, UpdateSampleItemSql, InsertSampleRatingSql,
        })
        {
            connection.RespondToNonQuery(sql, 1);
        }

        return connection;
    }

    private static DataTable HeaderTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldFirstEditDate", typeof(DateTime));
        table.Columns.Add("fldLastEditDate", typeof(DateTime));
        table.Columns.Add("fldDistribution", typeof(string));
        table.Columns.Add("fldSchemeIdentifier", typeof(string));
        table.Columns.Add("fldSchemeName", typeof(string));
        table.Columns.Add("fldDeadlineDate", typeof(DateTime));
        return table;
    }

    private static DataTable SampleResultTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldSampleResultItemId", typeof(Guid));
        table.Columns.Add("fldResult", typeof(string));
        return table;
    }

    private static DataTable MethodResultTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldTestMethodItemId", typeof(Guid));
        table.Columns.Add("fldResult", typeof(string));
        return table;
    }

    private static DataSet ResultSets(DataTable header, DataTable sampleResults, DataTable methodResults)
    {
        var dataSet = new DataSet();
        dataSet.Tables.Add(header);
        dataSet.Tables.Add(sampleResults);
        dataSet.Tables.Add(methodResults);
        return dataSet;
    }

    // ---------- GetActualResultsAsync ----------

    [Fact]
    public async Task GetActualResultsAsync_NoHeaderRow_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetActualResultsSql, ResultSets(HeaderTable(), SampleResultTable(), MethodResultTable()));

        Assert.Null(await repository.GetActualResultsAsync(ParticipantId));
    }

    [Fact]
    public async Task GetActualResultsAsync_MapsHeaderSampleResultsAndMethodItemResults()
    {
        var (repository, connection) = CreateRepository();
        var sampleItem1 = Guid.NewGuid();
        var sampleItem2 = Guid.NewGuid();
        var methodItem = Guid.NewGuid();

        var header = HeaderTable();
        header.Rows.Add(First, Last, "D1/S1", "BT", "Brucella", new DateTime(2026, 6, 30));
        var samples = SampleResultTable();
        samples.Rows.Add(sampleItem1, "12");
        samples.Rows.Add(sampleItem2, DBNull.Value);
        var methods = MethodResultTable();
        methods.Rows.Add(methodItem, "37C");
        connection.RespondToQuery(GetActualResultsSql, ResultSets(header, samples, methods));

        var data = await repository.GetActualResultsAsync(ParticipantId);

        Assert.NotNull(data);
        Assert.Equal((First, Last), (data.Header.FirstEditDate, data.Header.LastEditDate));
        Assert.Equal(("D1/S1", "BT", "Brucella"), (data.Header.Distribution, data.Header.SchemeIdentifier, data.Header.SchemeName));
        Assert.Equal(new DateTime(2026, 6, 30), data.Header.DeadlineDate);
        Assert.Equal([sampleItem1, sampleItem2], data.SampleResults.Select(r => r.SampleResultItemId));
        Assert.Equal(["12", null], data.SampleResults.Select(r => r.Result));
        var method = Assert.Single(data.MethodItemResults);
        Assert.Equal((methodItem, "37C"), (method.TestMethodItemId, method.Result));
        Assert.Equal(ParticipantId, Assert.Single(connection.ExecutedCommands).ParameterValue("ActualResultsId"));
    }

    [Fact]
    public async Task GetActualResultsAsync_NeverSavedParticipant_HasNullFirstEditDateAndNoResults()
    {
        var (repository, connection) = CreateRepository();
        var header = HeaderTable();
        header.Rows.Add(DBNull.Value, DBNull.Value, "D1/S1", "BT", "Brucella", new DateTime(2026, 6, 30));
        connection.RespondToQuery(GetActualResultsSql, ResultSets(header, SampleResultTable(), MethodResultTable()));

        var data = await repository.GetActualResultsAsync(ParticipantId);

        Assert.Null(data!.Header.FirstEditDate);
        Assert.Empty(data.SampleResults);
        Assert.Empty(data.MethodItemResults);
    }

    // ---------- GetRatingIdsAsync ----------

    [Fact]
    public async Task GetRatingIdsAsync_ReturnsTheRatingIdsInTheOrderTheProcedureReturnsThem()
    {
        var (repository, connection) = CreateRepository();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var table = new DataTable();
        table.Columns.Add("fldRatingId", typeof(Guid));
        table.Rows.Add(first);
        table.Rows.Add(second);
        connection.RespondToQuery(GetRatingIdsSql, table);

        var ids = await repository.GetRatingIdsAsync();

        Assert.Equal([first, second], ids);
    }

    [Fact]
    public async Task GetRatingIdsAsync_NoRatings_ReturnsEmpty()
    {
        var (repository, connection) = CreateRepository();
        var table = new DataTable();
        table.Columns.Add("fldRatingId", typeof(Guid));
        connection.RespondToQuery(GetRatingIdsSql, table);

        Assert.Empty(await repository.GetRatingIdsAsync());
    }

    // ---------- SaveActualResultsAsync ----------

    [Fact]
    public async Task SaveActualResultsAsync_NewSet_InsertsHeaderThenMethodItemsThenSampleItemsThenRatingsInOneCommittedTransaction()
    {
        var (repository, connection) = CreateRepository();
        RespondToAllWrites(connection);
        var methodId = Guid.NewGuid();
        var sampleItem1 = Guid.NewGuid();
        var sampleItem2 = Guid.NewGuid();
        var sample1 = Guid.NewGuid();
        var sample2 = Guid.NewGuid();
        var rating = Guid.NewGuid();

        await repository.SaveActualResultsAsync(new ActualResultsWrite(
            ParticipantId, IsNew: true, First, First,
            [new ActualResultWriteItem(methodId, "37C", IsInsert: true)],
            [new ActualResultWriteItem(sampleItem1, "12", IsInsert: true), new ActualResultWriteItem(sampleItem2, "", IsInsert: true)],
            [sample1, sample2],
            rating,
            "APHA\\jbloggs",
            string.Empty));

        Assert.Equal(
            [InsertActualResultsSql, InsertMethodItemSql, InsertSampleItemSql, InsertSampleItemSql, InsertSampleRatingSql, InsertSampleRatingSql],
            connection.ExecutedCommands.Select(c => c.CommandText));
        Assert.True(connection.LastTransaction!.Committed);
        Assert.False(connection.LastTransaction.RolledBack);

        var header = connection.ExecutedCommands[0];
        Assert.Equal(ParticipantId, header.ParameterValue("ActualResultsId"));
        Assert.Equal(First, header.ParameterValue("FirstEditDate"));
        Assert.Equal(First, header.ParameterValue("LastEditDate"));

        var method = connection.ExecutedCommands[1];
        Assert.Equal((ParticipantId, methodId, "37C"), (method.ParameterValue("ActualResultsId"), method.ParameterValue("TestMethodItemId"), method.ParameterValue("Result")));

        var sample = connection.ExecutedCommands[2];
        Assert.Equal((ParticipantId, sampleItem1, "12"), (sample.ParameterValue("ActualResultsId"), sample.ParameterValue("SampleResultItemId"), sample.ParameterValue("Result")));
        Assert.Equal(sampleItem2, connection.ExecutedCommands[3].ParameterValue("SampleResultItemId"));

        var firstRating = connection.ExecutedCommands[4];
        Assert.Equal((sample1, ParticipantId, rating), (firstRating.ParameterValue("SampleId"), firstRating.ParameterValue("ParticipantId"), firstRating.ParameterValue("RatingId")));
        Assert.Equal(sample2, connection.ExecutedCommands[5].ParameterValue("SampleId"));
    }

    [Fact]
    public async Task SaveActualResultsAsync_ExistingSet_UpdatesHeaderAndAuditsEachChangedRowBeforeItsUpdate()
    {
        var (repository, connection) = CreateRepository();
        RespondToAllWrites(connection);
        var methodId = Guid.NewGuid();
        var sampleItem = Guid.NewGuid();

        await repository.SaveActualResultsAsync(new ActualResultsWrite(
            ParticipantId, IsNew: false, First, Last,
            [new ActualResultWriteItem(methodId, "38C", IsInsert: false)],
            [new ActualResultWriteItem(sampleItem, "13", IsInsert: false)],
            [],
            null,
            "APHA\\jbloggs",
            "Lab corrected values"));

        Assert.Equal(
            [UpdateActualResultsSql, AuditMethodItemSql, UpdateMethodItemSql, AuditSampleItemSql, UpdateSampleItemSql],
            connection.ExecutedCommands.Select(c => c.CommandText));
        Assert.True(connection.LastTransaction!.Committed);

        var header = connection.ExecutedCommands[0];
        Assert.Equal(First, header.ParameterValue("FirstEditDate"));
        Assert.Equal(Last, header.ParameterValue("LastEditDate"));

        var methodAudit = connection.ExecutedCommands[1];
        Assert.Equal(
            (ParticipantId, methodId, "APHA\\jbloggs", "Lab corrected values", "38C"),
            (methodAudit.ParameterValue("ActualResultsId"), methodAudit.ParameterValue("TestMethodItemId"), methodAudit.ParameterValue("AuditWho"),
             methodAudit.ParameterValue("AuditWhy"), methodAudit.ParameterValue("Result")));
        Assert.Equal("38C", connection.ExecutedCommands[2].ParameterValue("Result"));

        var sampleAudit = connection.ExecutedCommands[3];
        Assert.Equal(
            (ParticipantId, sampleItem, "APHA\\jbloggs", "Lab corrected values", "13"),
            (sampleAudit.ParameterValue("ActualResultsId"), sampleAudit.ParameterValue("SampleResultItemId"), sampleAudit.ParameterValue("AuditWho"),
             sampleAudit.ParameterValue("AuditWhy"), sampleAudit.ParameterValue("Result")));
        Assert.Equal(sampleItem, connection.ExecutedCommands[4].ParameterValue("SampleResultItemId"));
    }

    [Fact]
    public async Task SaveActualResultsAsync_MixOfInsertsAndUpdates_AuditsOnlyUpdatedRowsAndKeepsRowOrder()
    {
        var (repository, connection) = CreateRepository();
        RespondToAllWrites(connection);

        await repository.SaveActualResultsAsync(new ActualResultsWrite(
            ParticipantId, IsNew: false, First, Last,
            [new ActualResultWriteItem(Guid.NewGuid(), "a", IsInsert: true), new ActualResultWriteItem(Guid.NewGuid(), "b", IsInsert: false)],
            [new ActualResultWriteItem(Guid.NewGuid(), "c", IsInsert: false), new ActualResultWriteItem(Guid.NewGuid(), "d", IsInsert: true)],
            [],
            null,
            "tester",
            "why"));

        Assert.Equal(
            [
                UpdateActualResultsSql,
                InsertMethodItemSql, AuditMethodItemSql, UpdateMethodItemSql,
                AuditSampleItemSql, UpdateSampleItemSql, InsertSampleItemSql,
            ],
            connection.ExecutedCommands.Select(c => c.CommandText));
    }

    [Fact]
    public async Task SaveActualResultsAsync_RatingSamplesWithoutADefaultRating_WritesNoRatings()
    {
        var (repository, connection) = CreateRepository();
        RespondToAllWrites(connection);

        await repository.SaveActualResultsAsync(new ActualResultsWrite(
            ParticipantId, IsNew: true, First, First, [], [], [Guid.NewGuid()], null, "tester", string.Empty));

        Assert.Equal([InsertActualResultsSql], connection.ExecutedCommands.Select(c => c.CommandText));
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task SaveActualResultsAsync_NothingElseToWrite_StillWritesTheHeaderAndCommits()
    {
        var (repository, connection) = CreateRepository();
        RespondToAllWrites(connection);

        await repository.SaveActualResultsAsync(new ActualResultsWrite(
            ParticipantId, IsNew: false, First, Last, [], [], [], null, "tester", "why"));

        Assert.Equal([UpdateActualResultsSql], connection.ExecutedCommands.Select(c => c.CommandText));
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task SaveActualResultsAsync_WhenAStatementFails_DoesNotCommit()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(InsertActualResultsSql, 1);
        // The method-item insert has no configured response, so the fake connection throws.

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveActualResultsAsync(new ActualResultsWrite(
            ParticipantId, IsNew: true, First, First,
            [new ActualResultWriteItem(Guid.NewGuid(), "x", IsInsert: true)], [], [], null, "tester", string.Empty)));

        Assert.False(connection.LastTransaction!.Committed);
    }
}
