using System.Data;
using PTL.Core.Distribution;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// spgMonthlyDistributionSchemeForAmendResults / spgaRating and the legacy AmendedResults
// MonthlyDistributionScheme.DataPortal_Update chain (spiAudit* before each audited update).
public class DistributionRepositoryAmendResultsTests
{
    public DistributionRepositoryAmendResultsTests() => DapperColumnMappings.Register();

    private const string GetSql = "EXEC dbo.spgMonthlyDistributionSchemeForAmendResults @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";
    private const string RatingsSql = "EXEC dbo.spgaRating";

    private const string RangeSql = "EXEC dbo.spuDistributionTestResultItem @DistributionTestResultItemId = @ResultItemId, @TestId = @TestId, @TestResultItemTypeId = @ResultItemTypeId, @Order = @Order, @IsRange = @IsRange";
    private const string ParticipantSql = "EXEC dbo.spuDistributionParticipantForAmendResults @DistributionParticipantId = @DistributionParticipantId, @NilReturnReason = @NilReturnReason";
    private const string AuditMethodSql = "EXEC dbo.spiAuditAmendedTestMethodItem @ActualResultsId = @ParticipantId, @TestMethodItemId = @MethodItemId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @Result = @Value, @ResultUpdate = 1";
    private const string MethodSql = "EXEC dbo.spuAmendedMethodItem @TestMethodItemId = @MethodItemId, @TestId = @TestId, @ParticipantId = @ParticipantId, @AmendedResult = @Value";
    private const string AuditResultSql = "EXEC dbo.spiAuditAmendedSampleResultItem @ActualResultsId = @ParticipantId, @TestResultItemId = @ResultItemId, @TestId = @TestId, @SampleId = @SampleId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @Result = @Value, @ResultUpdate = 1";
    private const string ResultSql = "EXEC dbo.spuAmendedResultItem @TestResultItemId = @ResultItemId, @SampleId = @SampleId, @TestId = @TestId, @ParticipantId = @ParticipantId, @AmendedResult = @Value";
    private const string AuditAssessmentSql = "EXEC dbo.spiAuditAssessmentItem @SampleReceiptId = @SampleReceiptId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @AssessmentComment = @Comment, @AssessmentCommentUpdate = @CommentChanged, @Score = @Score, @ScoreUpdate = @ScoreChanged";
    private const string AssessmentSql = "EXEC dbo.spuAmendedAssessmentItem @SampleReceiptId = @SampleReceiptId, @AssessmentComment = @Comment, @AssessmentScore = @Score";
    private const string RatingSql = "EXEC dbo.spuSampleRating @SampleRatingId = @SampleRatingId, @RatingId = @RatingId";
    private const string AuditCategorySql = "EXEC dbo.spiAuditAmendedCategoryItem @ActualResultsId = @ParticipantId, @CategoryItemId = @CategoryItemId, @SampleTestId = @SampleTestId, @TestId = @TestId, @SampleId = @SampleId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @Result = @Score, @ResultUpdate = 1";
    private const string CategorySql = "EXEC dbo.spuAmendedCategoryItem @CategoryItemId = @CategoryItemId, @SampleId = @SampleId, @TestId = @TestId, @ParticipantId = @ParticipantId, @AmendedCategoryScore = @Score, @SampleTestId = @SampleTestId";
    private const string AuditIntendedResultSql = "EXEC dbo.spiAuditAmendSampleResultItem @TestResultItemId = @ResultItemId, @TestId = @TestId, @SampleId = @SampleId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @IntendedResult = @Value, @IntendedResultUpdate = @ValueChanged, @IntendedResult2 = @Value2, @IntendedResult2Update = @Value2Changed";
    private const string IntendedResultSql = "EXEC dbo.spuDistributionTestResultItemForAmendResults @TestResultItemId = @ResultItemId, @SampleId = @SampleId, @TestId = @TestId, @IntendedResult = @Value, @IntendedResult2 = @Value2";
    private const string AuditIntendedMethodSql = "EXEC dbo.spiAuditAmendTestMethodItem @DistributionTestMethodItemId = @MethodItemId, @AuditWho = @AuditWho, @AuditWhy = @AuditWhy, @IntendedResult = @Value, @IntendedResultUpdate = 1";
    private const string IntendedMethodSql = "EXEC dbo.spuDistributionTestMethodItemForAmendResults @TestMethodItemId = @MethodItemId, @TestId = @TestId, @IntendedResult = @Value";

    private static readonly Guid SchemeId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TestId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid MethodId = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid ResultId = Guid.Parse("44444444-0000-0000-0000-000000000001");
    private static readonly Guid ResultTypeId = Guid.Parse("44444444-1111-0000-0000-000000000001");
    private static readonly Guid CategoryId = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly Guid SampleId = Guid.Parse("66666666-0000-0000-0000-000000000001");
    private static readonly Guid SampleTestId = Guid.Parse("66666666-1111-0000-0000-000000000001");
    private static readonly Guid ParticipantId = Guid.Parse("77777777-0000-0000-0000-000000000001");
    private static readonly Guid OtherParticipantId = Guid.Parse("77777777-0000-0000-0000-000000000002");
    private static readonly Guid SampleRatingId = Guid.Parse("88888888-0000-0000-0000-000000000001");
    private static readonly Guid RatingId = Guid.Parse("88888888-1111-0000-0000-000000000001");
    private static readonly Guid ReceiptId = Guid.Parse("99999999-0000-0000-0000-000000000001");
    private static readonly Guid Unknown = Guid.Parse("00000000-0000-0000-0000-0000000000ff");
    private static readonly DateTime Day = new(2026, 5, 10);

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static DataTable Table(params (string Name, Type Type)[] columns)
    {
        var table = new DataTable();
        foreach (var (name, type) in columns)
        {
            table.Columns.Add(name, type);
        }

        return table;
    }

    private static DataTable Rows(DataTable table, params object[][] rows)
    {
        foreach (var row in rows)
        {
            table.Rows.Add(row);
        }

        return table;
    }

    private static DataTable Header(params object[][] rows) => Rows(
        Table(("fldMonthlyDistributionSchemeId", typeof(Guid)), ("fldSchemeName", typeof(string)), ("fldSchemeIdentifier", typeof(string)),
            ("fldDistributionReference", typeof(string)), ("fldDistributionReferenceSuffix", typeof(string)), ("fldScheduleCode", typeof(string)),
            ("fldStoreRatings", typeof(bool)), ("fldRequiresAssessment", typeof(bool))),
        rows);

    // The 13 result sets after the header, in the procedure's order and with its column names. Each set has an
    // orphan row whose parent test/participant was not returned; they must be dropped. Duplicate keys keep the first.
    private static DataSet Sets(DataTable header, bool withRows)
    {
        object[][] When(params object[][] rows) => withRows ? rows : [];

        var dataSet = new DataSet();
        dataSet.Tables.AddRange(
        [
            header,
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestType", typeof(string)), ("fldAssessmentSubmitted", typeof(DateTime))),
                When([TestId, "Culture", Day], [Unknown, DBNull.Value, DBNull.Value])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestMethodItemId", typeof(Guid)), ("fldTestMethodItemType", typeof(string)),
                    ("fldFormatString", typeof(string)), ("fldMessage", typeof(string))),
                When([TestId, MethodId, "Titre", "^[0-9]+$", "Numeric"], [TestId, Guid.NewGuid(), DBNull.Value, DBNull.Value, DBNull.Value],
                    [Guid.NewGuid(), Guid.NewGuid(), "Orphan", "", ""])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)), ("fldTestResultItemType", typeof(string)),
                    ("fldFormatString", typeof(string)), ("fldMessage", typeof(string)), ("fldIsRange", typeof(bool)), ("fldOrder", typeof(int)),
                    ("fldTestResultItemTypeId", typeof(Guid))),
                When([TestId, ResultId, "Count", "Pos|Neg", "List", true, 3, ResultTypeId],
                    [TestId, Guid.NewGuid(), DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, ResultTypeId],
                    [Guid.NewGuid(), Guid.NewGuid(), "Orphan", "", "", false, 1, ResultTypeId])),
            Rows(Table(("fldCategoryItemId", typeof(Guid)), ("fldTestId", typeof(Guid)), ("fldCategoryItemTypeId", typeof(Guid)), ("fldName", typeof(string)), ("fldOrder", typeof(int))),
                When([CategoryId, TestId, Guid.NewGuid(), "Clinical", 1], [Guid.NewGuid(), TestId, Guid.NewGuid(), DBNull.Value, 2],
                    [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Orphan", 3])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldSampleNumber", typeof(string))),
                When([TestId, SampleId, "S01"], [TestId, Guid.NewGuid(), DBNull.Value], [Guid.NewGuid(), Guid.NewGuid(), "Orphan"])),
            Rows(Table(("fldDistributionParticipantId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldLabCode", typeof(string)),
                    ("fldDeadlineChangeReason", typeof(string)), ("fldNilReturnReason", typeof(string))),
                When([ParticipantId, Guid.NewGuid(), "L001", "late", "Lost"], [OtherParticipantId, Guid.NewGuid(), DBNull.Value, DBNull.Value, DBNull.Value])),
            Rows(Table(("fldSampleRatingId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldRatingId", typeof(Guid))),
                When([SampleRatingId, ParticipantId, SampleId, RatingId], [Guid.NewGuid(), ParticipantId, SampleId, Guid.NewGuid()],
                    [Guid.NewGuid(), Unknown, SampleId, RatingId])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldTestMethodItemId", typeof(Guid)),
                    ("fldActualResult", typeof(string)), ("fldAmendedResult", typeof(string))),
                When([TestId, ParticipantId, MethodId, "6", "7"], [TestId, ParticipantId, MethodId, "6", "dup"],
                    [TestId, OtherParticipantId, MethodId, "6", DBNull.Value], [TestId, Unknown, MethodId, "6", "6"])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)),
                    ("fldActualResult", typeof(string)), ("fldAmendedResult", typeof(string))),
                When([TestId, SampleId, ParticipantId, ResultId, "Pos", "Neg"], [TestId, SampleId, OtherParticipantId, ResultId, "Pos", DBNull.Value],
                    [TestId, SampleId, Unknown, ResultId, "Pos", "Pos"])),
            Rows(Table(("fldSampleReceiptId", typeof(Guid)), ("fldAssessmentComment", typeof(string)), ("fldScore", typeof(int)),
                    ("fldParticipantId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldTestId", typeof(Guid))),
                When([ReceiptId, "Good", 4, ParticipantId, SampleId, TestId], [Guid.NewGuid(), DBNull.Value, DBNull.Value, OtherParticipantId, SampleId, TestId],
                    [Guid.NewGuid(), "x", 1, Unknown, SampleId, TestId])),
            Rows(Table(("fldParticipantId", typeof(Guid)), ("fldCategoryItemId", typeof(Guid)), ("fldActualCategoryScore", typeof(int)),
                    ("fldAmendedCategoryScore", typeof(int)), ("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldSampleTestId", typeof(Guid))),
                When([ParticipantId, CategoryId, 2, 3, TestId, SampleId, SampleTestId], [OtherParticipantId, CategoryId, 2, DBNull.Value, TestId, SampleId, SampleTestId],
                    [Unknown, CategoryId, 2, 2, TestId, SampleId, SampleTestId])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestMethodItemId", typeof(Guid)), ("fldIntendedResult", typeof(string))),
                When([TestId, MethodId, "5"], [TestId, MethodId, "dup"], [TestId, Unknown, DBNull.Value])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)),
                    ("fldIntendedResult", typeof(string)), ("fldIntendedResult2", typeof(string))),
                When([TestId, SampleId, ResultId, "Pos", "Neg"], [TestId, Unknown, ResultId, DBNull.Value, DBNull.Value])),
        ]);
        return dataSet;
    }

    // ---- Fetch --------------------------------------------------------------------------------

    [Fact]
    public async Task GetAmendResultsAsync_NoHeader_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetSql, Sets(Header(), withRows: true));

        Assert.Null(await repository.GetAmendResultsAsync(SchemeId));

        var query = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(SchemeId, query.ParameterValue("MonthlyDistributionSchemeId"));
    }

    [Fact]
    public async Task GetAmendResultsAsync_MapsTheHeader()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetSql, Sets(Header([SchemeId, "Brucella", "BT", "D1", "a", "S1", true, true]), withRows: false));

        var source = await repository.GetAmendResultsAsync(SchemeId);

        Assert.NotNull(source);
        Assert.Equal(SchemeId, source.MonthlyDistributionSchemeId);
        Assert.Equal(("Brucella", "BT"), (source.SchemeName, source.SchemeIdentifier));
        Assert.Equal(("D1", "a", "S1"), (source.DistributionReference, source.DistributionReferenceSuffix, source.ScheduleCode));
        Assert.Equal("D1a/S1", source.DistributionReferenceFull);
        Assert.True(source.StoreRatings);
        Assert.True(source.RequiresAssessment);
        Assert.Empty(source.Tests);
        Assert.Empty(source.Participants);
        Assert.Empty(source.IntendedMethodResults);
        Assert.Empty(source.IntendedResultItemResults);
    }

    [Fact]
    public async Task GetAmendResultsAsync_NullHeaderColumns_MapToEmptyDefaults()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetSql, Sets(
            Header([SchemeId, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value]), withRows: false));

        var source = await repository.GetAmendResultsAsync(SchemeId);

        Assert.NotNull(source);
        Assert.Equal(string.Empty, source.SchemeName + source.SchemeIdentifier + source.DistributionReference + source.DistributionReferenceSuffix + source.ScheduleCode);
        Assert.Equal("/", source.DistributionReferenceFull);
        Assert.False(source.StoreRatings);
        Assert.False(source.RequiresAssessment);
    }

    [Fact]
    public async Task GetAmendResultsAsync_MapsTestsAndTheirItemsAndSamples_DroppingOrphans()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetSql, Sets(Header([SchemeId, "B", "BT", "D1", "", "S1", false, false]), withRows: true));

        var source = await repository.GetAmendResultsAsync(SchemeId);

        Assert.Equal(2, source!.Tests.Count);
        var test = source.Tests[0];
        Assert.Equal((TestId, "Culture", true), (test.TestId, test.Name, test.IsAssessmentSubmitted));
        Assert.Equal((Unknown, string.Empty, false), (source.Tests[1].TestId, source.Tests[1].Name, source.Tests[1].IsAssessmentSubmitted));

        Assert.Equal(2, test.MethodItems.Count);
        Assert.Equal(new AmendSourceItem(MethodId, "Titre", "^[0-9]+$", "Numeric"), test.MethodItems[0]);
        Assert.Equal((string.Empty, string.Empty, string.Empty), (test.MethodItems[1].Name, test.MethodItems[1].FormatString, test.MethodItems[1].Message));

        Assert.Equal(2, test.ResultItems.Count);
        Assert.Equal(new AmendSourceItem(ResultId, "Count", "Pos|Neg", "List", true, ResultTypeId, 3), test.ResultItems[0]);
        var nullResult = test.ResultItems[1];
        Assert.Equal((string.Empty, string.Empty, string.Empty, false, 0), (nullResult.Name, nullResult.FormatString, nullResult.Message, nullResult.IsRange, nullResult.Order));

        Assert.Equal([(CategoryId, "Clinical"), (test.CategoryItems[1].ItemId, string.Empty)], test.CategoryItems.Select(c => (c.ItemId, c.Name)));
        Assert.Equal([(SampleId, "S01"), (test.Samples[1].SampleId, string.Empty)], test.Samples.Select(s => (s.SampleId, s.SampleNumber)));
        Assert.Empty(source.Tests[1].MethodItems);
    }

    [Fact]
    public async Task GetAmendResultsAsync_MapsParticipantsAndTheirResults_KeyedByDistributionParticipant()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetSql, Sets(Header([SchemeId, "B", "BT", "D1", "", "S1", false, false]), withRows: true));

        var source = await repository.GetAmendResultsAsync(SchemeId);

        Assert.Equal([ParticipantId, OtherParticipantId], source!.Participants.Select(p => p.DistributionParticipantId));
        var lab1 = source.Participants[0];
        Assert.Equal(("L001", "Lost"), (lab1.LabCode, lab1.NilReturnReason));
        Assert.Equal(new Dictionary<Guid, (Guid, Guid)> { [SampleId] = (SampleRatingId, RatingId) }, lab1.Ratings);
        Assert.Equal(new Dictionary<Guid, string> { [MethodId] = "7" }, lab1.MethodResults);
        Assert.Equal(new Dictionary<(Guid, Guid), string> { [(SampleId, ResultId)] = "Neg" }, lab1.ResultItemResults);
        Assert.Equal(new Dictionary<(Guid, Guid), (Guid, string, int)> { [(TestId, SampleId)] = (ReceiptId, "Good", 4) }, lab1.Assessments);
        Assert.Equal(new Dictionary<(Guid, Guid), (int, Guid)> { [(SampleId, CategoryId)] = (3, SampleTestId) }, lab1.CategoryResults);

        var other = source.Participants[1];
        Assert.Equal((string.Empty, string.Empty), (other.LabCode, other.NilReturnReason));
        Assert.Empty(other.Ratings);
        Assert.Equal(string.Empty, other.MethodResults[MethodId]);
        Assert.Equal(string.Empty, other.ResultItemResults[(SampleId, ResultId)]);
        Assert.Equal((string.Empty, 0), (other.Assessments[(TestId, SampleId)].Comment, other.Assessments[(TestId, SampleId)].Score));
        Assert.Equal(0, other.CategoryResults[(SampleId, CategoryId)].Score);
    }

    [Fact]
    public async Task GetAmendResultsAsync_MapsTheIntendedValues_FirstRowWins()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetSql, Sets(Header([SchemeId, "B", "BT", "D1", "", "S1", false, false]), withRows: true));

        var source = await repository.GetAmendResultsAsync(SchemeId);

        Assert.Equal(new Dictionary<Guid, string> { [MethodId] = "5", [Unknown] = string.Empty }, source!.IntendedMethodResults);
        Assert.Equal(
            new Dictionary<(Guid, Guid), (string, string)> { [(SampleId, ResultId)] = ("Pos", "Neg"), [(Unknown, ResultId)] = (string.Empty, string.Empty) },
            source.IntendedResultItemResults);
    }

    [Fact]
    public async Task GetRatingOptionsAsync_MapsTheRatingsInProcedureOrder()
    {
        var (repository, connection) = CreateRepository();
        var other = Guid.NewGuid();
        connection.RespondToQuery(RatingsSql, Rows(Table(("fldRatingId", typeof(Guid)), ("fldRating", typeof(string))), [RatingId, "2"], [other, DBNull.Value]));

        var options = await repository.GetRatingOptionsAsync();

        Assert.Equal([new AmendRatingOption(RatingId, "2"), new AmendRatingOption(other, string.Empty)], options);
        Assert.Equal(RatingsSql, Assert.Single(connection.ExecutedCommands).CommandText);
    }

    // ---- Save ---------------------------------------------------------------------------------

    private static FakeDbConnection RespondToEveryWrite(FakeDbConnection connection)
    {
        foreach (var sql in new[]
        {
            RangeSql, ParticipantSql, AuditMethodSql, MethodSql, AuditResultSql, ResultSql, AuditAssessmentSql, AssessmentSql, RatingSql,
            AuditCategorySql, CategorySql, AuditIntendedResultSql, IntendedResultSql, AuditIntendedMethodSql, IntendedMethodSql,
        })
        {
            connection.RespondToNonQuery(sql, 1);
        }

        return connection;
    }

    private static AmendResultsWrite FullWrite(string auditWhy) => new(
        "DOMAIN\\tc",
        auditWhy,
        [new AmendRangeWrite(ResultId, TestId, ResultTypeId, 3, true)],
        [
            new AmendParticipantWrite(
                ParticipantId,
                "Lost",
                [new AmendMethodResultWrite(TestId, MethodId, "7")],
                [new AmendResultItemWrite(TestId, SampleId, ResultId, "Neg")],
                [new AmendAssessmentWrite(ReceiptId, "Good", true, 4, false)],
                [new AmendRatingWrite(SampleRatingId, RatingId)],
                [new AmendCategoryWrite(TestId, SampleId, CategoryId, SampleTestId, 5)]),
            new AmendParticipantWrite(OtherParticipantId, "Damaged", [], [], [], [], []),
        ],
        [new AmendIntendedResultWrite(TestId, SampleId, ResultId, "Pos", true, "Neg", false)],
        [new AmendIntendedMethodWrite(TestId, MethodId, "5")]);

    [Fact]
    public async Task SaveAmendResultsAsync_NullWrite_Throws()
    {
        var (repository, _) = CreateRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.SaveAmendResultsAsync(null!));
    }

    [Fact]
    public async Task SaveAmendResultsAsync_WithAReason_AuditsBeforeEachAuditedUpdateInTheLegacyOrderInOneTransaction()
    {
        var (repository, connection) = CreateRepository();
        RespondToEveryWrite(connection);

        await repository.SaveAmendResultsAsync(FullWrite("Typo"));

        var transaction = connection.LastTransaction!;
        Assert.True(transaction.Committed);
        Assert.False(transaction.RolledBack);
        Assert.Equal(
            [
                RangeSql,
                ParticipantSql, AuditMethodSql, MethodSql, AuditResultSql, ResultSql, AuditAssessmentSql, AssessmentSql, RatingSql, AuditCategorySql, CategorySql,
                ParticipantSql,
                AuditIntendedResultSql, IntendedResultSql,
                AuditIntendedMethodSql, IntendedMethodSql,
            ],
            connection.ExecutedCommands.Select(c => c.CommandText));
        Assert.All(connection.ExecutedCommands, c => Assert.Same(transaction, c.Transaction));
        Assert.All(connection.ExecutedCommands.Where(c => c.CommandText.Contains("spiAudit", StringComparison.Ordinal)), c =>
        {
            Assert.Equal("DOMAIN\\tc", c.ParameterValue("AuditWho"));
            Assert.Equal("Typo", c.ParameterValue("AuditWhy"));
        });
    }

    [Fact]
    public async Task SaveAmendResultsAsync_WithoutAReason_RunsOnlyTheUpdates()
    {
        var (repository, connection) = CreateRepository();
        RespondToEveryWrite(connection);

        await repository.SaveAmendResultsAsync(FullWrite(string.Empty));

        Assert.True(connection.LastTransaction!.Committed);
        Assert.Equal(
            [RangeSql, ParticipantSql, MethodSql, ResultSql, AssessmentSql, RatingSql, CategorySql, ParticipantSql, IntendedResultSql, IntendedMethodSql],
            connection.ExecutedCommands.Select(c => c.CommandText));
    }

    [Fact]
    public async Task SaveAmendResultsAsync_PassesEachProcedureItsParameters()
    {
        var (repository, connection) = CreateRepository();
        RespondToEveryWrite(connection);

        await repository.SaveAmendResultsAsync(FullWrite("Typo"));

        FakeDbCommand Command(string sql, int occurrence = 0) => connection.ExecutedCommands.Where(c => c.CommandText == sql).ElementAt(occurrence);

        var range = Command(RangeSql);
        Assert.Equal((ResultId, TestId, ResultTypeId, 3, true),
            (range.ParameterValue("ResultItemId"), range.ParameterValue("TestId"), range.ParameterValue("ResultItemTypeId"), range.ParameterValue("Order"), range.ParameterValue("IsRange")));

        Assert.Equal((ParticipantId, "Lost"), (Command(ParticipantSql).ParameterValue("DistributionParticipantId"), Command(ParticipantSql).ParameterValue("NilReturnReason")));
        Assert.Equal((OtherParticipantId, "Damaged"), (Command(ParticipantSql, 1).ParameterValue("DistributionParticipantId"), Command(ParticipantSql, 1).ParameterValue("NilReturnReason")));

        foreach (var sql in new[] { AuditMethodSql, MethodSql })
        {
            var method = Command(sql);
            Assert.Equal((MethodId, ParticipantId, "7"),
                (method.ParameterValue("MethodItemId"), method.ParameterValue("ParticipantId"), method.ParameterValue("Value")));
        }

        Assert.Equal(TestId, Command(MethodSql).ParameterValue("TestId"));

        foreach (var sql in new[] { AuditResultSql, ResultSql })
        {
            var result = Command(sql);
            Assert.Equal((TestId, SampleId, ResultId, ParticipantId, "Neg"),
                (result.ParameterValue("TestId"), result.ParameterValue("SampleId"), result.ParameterValue("ResultItemId"), result.ParameterValue("ParticipantId"), result.ParameterValue("Value")));
        }

        var assessment = Command(AuditAssessmentSql);
        Assert.Equal((ReceiptId, "Good", true, 4, false),
            (assessment.ParameterValue("SampleReceiptId"), assessment.ParameterValue("Comment"), assessment.ParameterValue("CommentChanged"),
                assessment.ParameterValue("Score"), assessment.ParameterValue("ScoreChanged")));
        Assert.Equal((ReceiptId, "Good", 4), (Command(AssessmentSql).ParameterValue("SampleReceiptId"), Command(AssessmentSql).ParameterValue("Comment"), Command(AssessmentSql).ParameterValue("Score")));

        Assert.Equal((SampleRatingId, RatingId), (Command(RatingSql).ParameterValue("SampleRatingId"), Command(RatingSql).ParameterValue("RatingId")));

        foreach (var sql in new[] { AuditCategorySql, CategorySql })
        {
            var category = Command(sql);
            Assert.Equal((TestId, SampleId, CategoryId, SampleTestId, 5, ParticipantId),
                (category.ParameterValue("TestId"), category.ParameterValue("SampleId"), category.ParameterValue("CategoryItemId"),
                    category.ParameterValue("SampleTestId"), category.ParameterValue("Score"), category.ParameterValue("ParticipantId")));
        }

        var intended = Command(AuditIntendedResultSql);
        Assert.Equal((TestId, SampleId, ResultId, "Pos", true, "Neg", false),
            (intended.ParameterValue("TestId"), intended.ParameterValue("SampleId"), intended.ParameterValue("ResultItemId"), intended.ParameterValue("Value"),
                intended.ParameterValue("ValueChanged"), intended.ParameterValue("Value2"), intended.ParameterValue("Value2Changed")));
        Assert.Equal(("Pos", "Neg"), (Command(IntendedResultSql).ParameterValue("Value"), Command(IntendedResultSql).ParameterValue("Value2")));

        foreach (var sql in new[] { AuditIntendedMethodSql, IntendedMethodSql })
        {
            var method = Command(sql);
            Assert.Equal((MethodId, "5"), (method.ParameterValue("MethodItemId"), method.ParameterValue("Value")));
        }

        Assert.Equal(TestId, Command(IntendedMethodSql).ParameterValue("TestId"));
    }

    [Fact]
    public async Task SaveAmendResultsAsync_NothingToWrite_StillCommitsAnEmptyTransaction()
    {
        var (repository, connection) = CreateRepository();

        await repository.SaveAmendResultsAsync(new AmendResultsWrite("who", "", [], [], [], []));

        Assert.Empty(connection.ExecutedCommands);
        Assert.True(connection.LastTransaction!.Committed);
    }
}
