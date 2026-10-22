using System.Data;
using PTL.Core.Distribution;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// spgMonthlyDistributionInfoForTestConsultant and spgMonthlyDistributionSchemeForTestConsultant - legacy
// YearInfoCollection/MonthlyDistributionInfoCollection.FetchCollection and the Test Consultant
// MonthlyDistributionScheme.FetchMonthlyDistributionScheme.
public class TestConsultantTabulationRepositoryTests
{
    public TestConsultantTabulationRepositoryTests() => DapperColumnMappings.Register();

    private const string MenuSql = "EXEC dbo.spgMonthlyDistributionInfoForTestConsultant @TestConsultantId = @TestConsultantId";
    private const string TabulationSql = "EXEC dbo.spgMonthlyDistributionSchemeForTestConsultant @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId, @GetFullTabulation = @GetFullTabulation";
    private const string NameSql = "SELECT dbo.fnGetTestConsultantNameById(@TestConsultantId)";

    private const string StateSql = """
        SELECT
            s.fldTestConsultant1 AS fldTestConsultant1Id,
            s.fldTestConsultant2 AS fldTestConsultant2Id,
            s.fldTestConsultant3 AS fldTestConsultant3Id,
            mds.fldIsCommentSubmitted AS fldIsCommentSubmitted,
            mds.fldTestConsultantComments AS fldTestConsultantComments,
            mds.fldTestConsultantCommentsAuthorId AS fldTestConsultantCommentsAuthorId
        FROM tblMonthlyDistributionScheme mds
        INNER JOIN tblScheme s ON mds.fldSchemeId = s.fldSchemeId
        WHERE mds.fldMonthlyDistributionSchemeId = @MonthlyDistributionSchemeId
        """;

    private static readonly Guid ConsultantId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid SchemeId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid OtherSchemeId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid MonthJune = Guid.Parse("33333333-0000-0000-0000-000000000006");
    private static readonly Guid MonthMay = Guid.Parse("33333333-0000-0000-0000-000000000005");
    private static readonly Guid TestId = Guid.Parse("44444444-0000-0000-0000-000000000001");
    private static readonly Guid MethodId = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly Guid ResultId = Guid.Parse("66666666-0000-0000-0000-000000000001");
    private static readonly Guid SampleId = Guid.Parse("77777777-0000-0000-0000-000000000001");
    private static readonly Guid ParticipantId = Guid.Parse("88888888-0000-0000-0000-000000000001");
    private static readonly Guid Unknown = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    private static (TestConsultantTabulationRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new TestConsultantTabulationRepository(new FakeDbConnectionFactory(connection)), connection);
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

    private static DataSet Sets(params DataTable[] tables)
    {
        var dataSet = new DataSet();
        dataSet.Tables.AddRange(tables);
        return dataSet;
    }

    private static void RespondToMultiLine(FakeDbConnection connection, string sql, DataTable table)
    {
        connection.RespondToQuery(sql.ReplaceLineEndings("\n"), table);
        connection.RespondToQuery(sql.ReplaceLineEndings("\r\n"), table);
    }

    // ---- Menu ---------------------------------------------------------------------------------

    private static DataTable MenuYears(params object[][] rows) => Rows(Table(("fldYearId", typeof(int))), rows);

    private static DataTable MenuMonths(params object[][] rows) =>
        Rows(Table(("fldMonthlyDistributionId", typeof(Guid)), ("fldYearId", typeof(int)), ("fldMonthId", typeof(int))), rows);

    private static DataTable MenuSchemes(params object[][] rows) => Rows(
        Table(("fldYearId", typeof(int)), ("fldMonthlyDistributionId", typeof(Guid)), ("fldMonthlyDistributionSchemeId", typeof(Guid)),
            ("fldSchemeName", typeof(string)), ("fldSchemeIdentifier", typeof(string)), ("fldDistributionReference", typeof(string)),
            ("fldDistributionReferenceSuffix", typeof(string)), ("fldScheduleCode", typeof(string)), ("fldResultsIssueTargetDate", typeof(DateTime)),
            ("fldTestConsultantComments", typeof(string)), ("fldIsCommentSubmitted", typeof(bool))),
        rows);

    [Fact]
    public async Task GetDistributionsAsync_CallsTheProcedureForTheConsultant()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(MenuSql, Sets(MenuYears(), MenuMonths(), MenuSchemes()));

        var distributions = await repository.GetDistributionsAsync(ConsultantId);

        Assert.Empty(distributions);
        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal((MenuSql, ConsultantId), (command.CommandText, command.ParameterValue("TestConsultantId")));
    }

    [Fact]
    public async Task GetDistributionsAsync_MapsSchemesWithTheirMonthNewestMonthFirst()
    {
        var (repository, connection) = CreateRepository();
        var target = new DateTime(2026, 7, 21);
        connection.RespondToQuery(MenuSql, Sets(
            MenuYears([2026]),
            MenuMonths([MonthJune, 2026, 6], [MonthMay, 2026, 5]),
            MenuSchemes(
                [2026, MonthMay, OtherSchemeId, "Brucella", "BT", "17001", "/BA", "PT0001", new DateTime(2026, 6, 1), "Draft", true],
                [2026, MonthJune, SchemeId, "Pasteurella", "PM", "17208", "/BA", "PT0074", target, DBNull.Value, false])));

        var distributions = await repository.GetDistributionsAsync(ConsultantId);

        Assert.Equal([SchemeId, OtherSchemeId], distributions.Select(d => d.MonthlyDistributionSchemeId));
        var june = distributions[0];
        Assert.Equal((2026, 6, "Pasteurella", "PM", "17208", "/BA", "PT0074", target, false),
            (june.YearId, june.MonthId, june.SchemeName, june.SchemeIdentifier, june.DistributionReference, june.DistributionReferenceSuffix, june.ScheduleCode, june.TargetDate, june.IsCommentSubmitted));
        Assert.Equal((5, true), (distributions[1].MonthId, distributions[1].IsCommentSubmitted));
    }

    [Fact]
    public async Task GetDistributionsAsync_NullColumns_MapToDefaultsAndUnknownMonthsAreSkipped()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(MenuSql, Sets(
            MenuYears([2026]),
            MenuMonths([MonthJune, 2026, 6]),
            MenuSchemes(
                [2026, MonthJune, SchemeId, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value],
                [2026, Unknown, OtherSchemeId, "Orphan", "OR", "1", "a", "S", new DateTime(2026, 1, 1), "x", false])));

        var distribution = Assert.Single(await repository.GetDistributionsAsync(ConsultantId));

        Assert.Equal(SchemeId, distribution.MonthlyDistributionSchemeId);
        Assert.All([distribution.SchemeName, distribution.SchemeIdentifier, distribution.DistributionReference, distribution.DistributionReferenceSuffix, distribution.ScheduleCode],
            value => Assert.Equal(string.Empty, value));
        Assert.Equal((default, false), (distribution.TargetDate, distribution.IsCommentSubmitted));
    }

    // ---- Tabulation ---------------------------------------------------------------------------

    private static readonly (string, Type)[] HeaderColumns =
    [
        ("fldMonthlyDistributionSchemeId", typeof(Guid)), ("fldSchemeName", typeof(string)), ("fldSchemeIdentifier", typeof(string)),
        ("fldDistributionReference", typeof(string)), ("fldDistributionReferenceSuffix", typeof(string)), ("fldDistributionDate", typeof(DateTime)),
        ("fldScheduleCode", typeof(string)), ("fldResultsIssueTargetDate", typeof(DateTime)), ("fldTestConsultantComments", typeof(string)),
        ("fldIsCommentSubmitted", typeof(bool)), ("fldTestConsultantCommentsAuthorId", typeof(Guid)), ("fldTestConsultantCommentsAuthor", typeof(string)),
        ("fldComments", typeof(string)), ("fldTestConsultant1Id", typeof(Guid)), ("fldShowRatings", typeof(bool)), ("fldStandardTabulationText", typeof(string)),
    ];

    private static DataSet TabulationSets(DataTable header, bool withRows)
    {
        object[][] When(params object[][] rows) => withRows ? rows : [];

        return Sets(
            header,
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestType", typeof(string))), When([TestId, "PCR"])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestMethodItemId", typeof(Guid)), ("fldTestMethodItemType", typeof(string))),
                When([TestId, MethodId, "Method Details"], [Unknown, Guid.NewGuid(), "Orphan"])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)), ("fldTestResultItemType", typeof(string))),
                When([TestId, ResultId, "Result"], [Unknown, Guid.NewGuid(), "Orphan"])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldSampleNumber", typeof(string)), ("fldTabulationDescription", typeof(string))),
                When([TestId, SampleId, "26/1", "Positive"], [Unknown, Guid.NewGuid(), "26/9", "Orphan"])),
            Rows(Table(("fldDistributionParticipantId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldLabCode", typeof(string)),
                    ("fldDeadlineChangeReason", typeof(string)), ("fldExtraDocumentsReturned", typeof(bool)), ("fldNilReturnReason", typeof(string))),
                When([Guid.NewGuid(), ParticipantId, "L001", "Postal delay", true, DBNull.Value])),
            Rows(Table(("fldSampleRatingId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldRating", typeof(string))),
                When([Guid.NewGuid(), SampleId, ParticipantId, "2"], [Guid.NewGuid(), SampleId, Unknown, "0"])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldTestMethodItemId", typeof(Guid)), ("fldActualResult", typeof(string)), ("fldAmendedResult", typeof(string))),
                When([TestId, ParticipantId, MethodId, "PCR", "Amended PCR"], [TestId, Unknown, MethodId, "X", "X"])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)),
                    ("fldActualResult", typeof(string)), ("fldAmendedResult", typeof(string))),
                When([TestId, SampleId, ParticipantId, ResultId, "Pos", "Positive"], [TestId, SampleId, Unknown, ResultId, "X", "X"])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestMethodItemId", typeof(Guid)), ("fldIntendedResult", typeof(string))),
                When([TestId, MethodId, "PCR"], [TestId, MethodId, "Duplicate"])),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)), ("fldIntendedResult", typeof(string)), ("fldIntendedResult2", typeof(string))),
                When([TestId, SampleId, ResultId, "1", "2"], [TestId, SampleId, ResultId, DBNull.Value, DBNull.Value])),
            Rows(Table(("fldSampleId", typeof(Guid)), ("fldSampleNumber", typeof(string)), ("fldTabulationDescription", typeof(string)), ("fldTestConsultantsDescription", typeof(string))),
                When([SampleId, "26/1", "Positive", "Sample details"], [Guid.NewGuid(), DBNull.Value, DBNull.Value, DBNull.Value])));
    }

    private static DataTable Header(params object[][] rows) => Rows(Table(HeaderColumns), rows);

    private static object[] FullHeaderRow(bool submitted = true) =>
        [SchemeId, "Brucella", "BT", "D1", "a", new DateTime(2026, 5, 3), "S1", new DateTime(2026, 7, 21), "<p>Comments</p>", submitted, ConsultantId, "A Author",
            "<p>Scheme</p>", ConsultantId, true, "<p>Standard</p>"];

    [Fact]
    public async Task GetTabulationAsync_NoHeader_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(TabulationSql, TabulationSets(Header(), false));

        Assert.Null(await repository.GetTabulationAsync(SchemeId, false));
        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal((SchemeId, false), (command.ParameterValue("MonthlyDistributionSchemeId"), command.ParameterValue("GetFullTabulation")));
    }

    [Fact]
    public async Task GetTabulationAsync_FullTabulation_AsksForTheFullProcedureVariant()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(TabulationSql, TabulationSets(Header(), false));

        await repository.GetTabulationAsync(SchemeId, true);

        Assert.Equal(true, Assert.Single(connection.ExecutedCommands).ParameterValue("GetFullTabulation"));
    }

    [Fact]
    public async Task GetTabulationAsync_MapsTheHeaderIntoTheTabulationSource()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(TabulationSql, TabulationSets(Header(FullHeaderRow()), true));

        var entity = await repository.GetTabulationAsync(SchemeId, true);

        Assert.NotNull(entity);
        Assert.True(entity.IsCommentSubmitted);
        var source = entity.Source;
        Assert.Equal((SchemeId, "D1a/S1", "BT", "Brucella"), (source.TabulationId, source.Distribution, source.SchemeIdentifier, source.SchemeName));
        Assert.Equal(("<p>Comments</p>", "A Author", "<p>Scheme</p>", "<p>Standard</p>", true, new DateTime(2026, 5, 3)),
            (source.TestConsultantComments, source.TestConsultantCommentsAuthor, source.Comments, source.StandardTabulationText, source.HasRatings, source.DistributionDate));
        Assert.False(source.IntendedResultsOnly);
    }

    [Fact]
    public async Task GetTabulationAsync_NullHeaderColumns_MapToDefaults()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(TabulationSql, TabulationSets(Header([SchemeId, .. Enumerable.Repeat<object>(DBNull.Value, 15)]), false));

        var entity = await repository.GetTabulationAsync(SchemeId, false);

        Assert.NotNull(entity);
        Assert.False(entity.IsCommentSubmitted);
        Assert.All([entity.Source.Distribution.Trim('/'), entity.Source.SchemeName, entity.Source.TestConsultantComments, entity.Source.Comments, entity.Source.StandardTabulationText],
            value => Assert.Equal(string.Empty, value));
        Assert.Empty(entity.Source.Tests);
    }

    [Fact]
    public async Task GetTabulationAsync_MapsTestsItemsSamplesAndParticipants()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(TabulationSql, TabulationSets(Header(FullHeaderRow()), true));

        var entity = await repository.GetTabulationAsync(SchemeId, true);

        var test = Assert.Single(entity!.Source.Tests);
        Assert.Equal(("PCR", MethodId, "Method Details"), (test.Name, test.MethodItems[0].ItemId, test.MethodItems[0].Name));
        Assert.Equal((ResultId, "Result"), (test.ResultItems[0].ItemId, test.ResultItems[0].Name));
        Assert.Equal((SampleId, "26/1", "Positive"), (test.Samples[0].SampleId, test.Samples[0].SampleNumber, test.Samples[0].TabulationDescription));
        Assert.Null(test.Samples[0].SampleTestId);
        var participant = Assert.Single(entity.Source.Participants);
        Assert.Equal((ParticipantId, "L001", string.Empty), (participant.ParticipantId, participant.LabCode, participant.NilReturnReason));
        Assert.Equal("2", participant.SampleRatings[SampleId]);
        Assert.Equal("Amended PCR", participant.MethodResults[MethodId]);
        Assert.Equal("Positive", participant.ResultItemResults[(SampleId, ResultId)]);
    }

    [Fact]
    public async Task GetTabulationAsync_MapsTheIntendedResults_KeepingTheFirstOfAnyDuplicate()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(TabulationSql, TabulationSets(Header(FullHeaderRow()), true));

        var source = (await repository.GetTabulationAsync(SchemeId, true))!.Source;

        Assert.Equal("PCR", source.IntendedMethodResults[MethodId]);
        Assert.Equal(("1", "2"), source.IntendedResultItemResults[(SampleId, ResultId)]);
    }

    [Fact]
    public async Task GetTabulationAsync_MapsTheParticipantAndSampleInformation()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(TabulationSql, TabulationSets(Header(FullHeaderRow()), true));

        var entity = await repository.GetTabulationAsync(SchemeId, true);

        Assert.Equal(new TestConsultantParticipantInformation("L001", true, "Postal delay"), Assert.Single(entity!.Participants));
        Assert.Equal(new TestConsultantSampleInformation("26/1", "Positive", "Sample details"), entity.SampleDetails[0]);
        Assert.Equal(new TestConsultantSampleInformation(string.Empty, string.Empty, string.Empty), entity.SampleDetails[1]);
    }

    // ---- Scheme state -------------------------------------------------------------------------

    private static DataTable StateTable(params object[][] rows) => Rows(
        Table(("fldTestConsultant1Id", typeof(Guid)), ("fldTestConsultant2Id", typeof(Guid)), ("fldTestConsultant3Id", typeof(Guid)),
            ("fldIsCommentSubmitted", typeof(bool)), ("fldTestConsultantComments", typeof(string)), ("fldTestConsultantCommentsAuthorId", typeof(Guid))),
        rows);

    [Fact]
    public async Task GetSchemeStateAsync_UnknownScheme_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        RespondToMultiLine(connection, StateSql, StateTable());

        Assert.Null(await repository.GetSchemeStateAsync(SchemeId));
        Assert.Equal(SchemeId, Assert.Single(connection.ExecutedCommands).ParameterValue("MonthlyDistributionSchemeId"));
    }

    [Fact]
    public async Task GetSchemeStateAsync_MapsTheAllocatedConsultantsAndTheCommentState()
    {
        var (repository, connection) = CreateRepository();
        var second = Guid.NewGuid();
        RespondToMultiLine(connection, StateSql, StateTable([ConsultantId, second, DBNull.Value, true, "<p>Hi</p>", ConsultantId]));

        var state = await repository.GetSchemeStateAsync(SchemeId);

        Assert.Equal(new TestConsultantSchemeState(ConsultantId, second, null, true, "<p>Hi</p>", ConsultantId), state);
        Assert.True(state!.IsAllocated(second));
        Assert.False(state.IsAllocated(Unknown));
    }

    [Fact]
    public async Task GetSchemeStateAsync_NullCommentColumns_MapToDefaults()
    {
        var (repository, connection) = CreateRepository();
        RespondToMultiLine(connection, StateSql, StateTable([DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value]));

        var state = await repository.GetSchemeStateAsync(SchemeId);

        Assert.Equal(new TestConsultantSchemeState(null, null, null, false, string.Empty, Guid.Empty), state);
        Assert.False(state!.IsAllocated(Guid.Empty));
    }

    // ---- Name ---------------------------------------------------------------------------------

    [Fact]
    public async Task GetTestConsultantNameAsync_ReturnsTheName()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(NameSql, Rows(Table(("Name", typeof(string))), ["A Author"]));

        Assert.Equal("A Author", await repository.GetTestConsultantNameAsync(ConsultantId));
        Assert.Equal(ConsultantId, Assert.Single(connection.ExecutedCommands).ParameterValue("TestConsultantId"));
    }

    [Fact]
    public async Task GetTestConsultantNameAsync_NoSuchConsultant_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(NameSql, Rows(Table(("Name", typeof(string))), [DBNull.Value]));

        Assert.Null(await repository.GetTestConsultantNameAsync(ConsultantId));
    }

    // ---- Inactive -----------------------------------------------------------------------------

    private const string IsInactiveSql =
        "SELECT CASE WHEN EXISTS (SELECT 1 FROM tblUsers WHERE fldUserId = @TestConsultantId AND fldIsInactive = 1) " +
        "OR EXISTS (SELECT 1 FROM tblExternalTestConsultant WHERE fldExternalTestConsultantId = @TestConsultantId AND fldIsInactive = 1) " +
        "THEN 1 ELSE 0 END";

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public async Task IsInactiveAsync_ReflectsTheInactiveFlagOfTheUserOrExternalConsultant(int scalar, bool expected)
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(IsInactiveSql, scalar);

        Assert.Equal(expected, await repository.IsInactiveAsync(ConsultantId));
        Assert.Equal(ConsultantId, Assert.Single(connection.ExecutedCommands).ParameterValue("TestConsultantId"));
    }
}
