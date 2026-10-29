using System.Data;
using PTL.Core.Distribution;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// spgYearByAssessorId, spgMonthlyDistributionInfoForAssessor and spgAssessmentItemByTestId - legacy Assessor YearInfoCollection,
// MonthlyDistributionInfoCollection and SampleInfoCollection - and the inline assignment check.
public class AssessorRepositoryTests
{
    public AssessorRepositoryTests() => DapperColumnMappings.Register();

    private const string YearsSql = "EXEC dbo.spgYearByAssessorId @AssessorId = @AssessorId";
    private const string MonthsSql = "EXEC dbo.spgMonthlyDistributionInfoForAssessor @YearId = @YearId, @AssessorId = @AssessorId";
    private const string ItemsSql = "EXEC dbo.spgAssessmentItemByTestId @TestId = @TestId";

    private const string AccessSql = """
        SELECT CASE WHEN @AssessorId IN (s.fldAssessor1, s.fldAssessor2, s.fldAssessor3, s.fldAssessor4) THEN 1 ELSE 0 END
        FROM tblDistributionTest dt
        INNER JOIN tblMonthlyDistributionScheme mds ON dt.fldDistributionSchemeId = mds.fldMonthlyDistributionSchemeId
        INNER JOIN tblScheme s ON s.fldSchemeId = mds.fldSchemeId
        WHERE dt.fldTestId = @TestId
        """;

    private static readonly Guid Assessor = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid Feb = Guid.Parse("33333333-0000-0000-0000-000000000002");
    private static readonly Guid Jan = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid SchemeA = Guid.Parse("22222222-0000-0000-0000-00000000000a");
    private static readonly Guid SchemeB = Guid.Parse("22222222-0000-0000-0000-00000000000b");
    private static readonly Guid SchemeC = Guid.Parse("22222222-0000-0000-0000-00000000000c");
    private static readonly Guid Test1 = Guid.Parse("44444444-0000-0000-0000-000000000001");
    private static readonly Guid Test2 = Guid.Parse("44444444-0000-0000-0000-000000000002");
    private static readonly Guid Test3 = Guid.Parse("44444444-0000-0000-0000-000000000003");
    private static readonly Guid Sample1 = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly Guid Sample2 = Guid.Parse("55555555-0000-0000-0000-000000000002");
    private static readonly Guid SampleTest1 = Guid.Parse("66666666-0000-0000-0000-000000000001");
    private static readonly Guid ParticipantA = Guid.Parse("77777777-0000-0000-0000-00000000000a");
    private static readonly Guid ParticipantB = Guid.Parse("77777777-0000-0000-0000-00000000000b");

    private static (AssessorRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new AssessorRepository(new FakeDbConnectionFactory(connection)), connection);
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

    private static DataTable Years(params object[][] rows) => Rows(Table(("fldYearId", typeof(int))), rows);

    private static DataTable Months(params object[][] rows) =>
        Rows(Table(("fldMonthlyDistributionId", typeof(Guid)), ("fldYearId", typeof(int)), ("fldMonthId", typeof(int))), rows);

    private static DataTable Distributions(params object[][] rows) => Rows(
        Table(("fldYearId", typeof(int)), ("fldMonthlyDistributionId", typeof(Guid)), ("fldMonthlyDistributionSchemeId", typeof(Guid)),
            ("fldSchemeName", typeof(string)), ("fldSchemeIdentifier", typeof(string)), ("fldDistributionReference", typeof(string)),
            ("fldDistributionReferenceSuffix", typeof(string)), ("fldScheduleCode", typeof(string))),
        rows);

    private static DataTable Tests(params object[][] rows) => Rows(
        Table(("fldTestId", typeof(Guid)), ("fldTestType", typeof(string)), ("fldMonthlyDistributionId", typeof(Guid)),
            ("fldMonthlyDistributionSchemeId", typeof(Guid)), ("fldAssessmentSubmitted", typeof(DateTime))),
        rows);

    private static DataTable ItemHeader(params object[][] rows) => Rows(
        Table(("fldAssessmentSubmitted", typeof(DateTime)), ("fldAssessorName", typeof(string)), ("fldSchemeName", typeof(string)),
            ("fldSchemeIdentifier", typeof(string)), ("fldDistributionReference", typeof(string)), ("fldDistributionReferenceSuffix", typeof(string)),
            ("fldScheduleCode", typeof(string)), ("fldTestName", typeof(string)), ("fldYearId", typeof(int)), ("fldMonthId", typeof(int))),
        rows);

    private static DataTable ItemSamples(params object[][] rows) =>
        Rows(Table(("fldSampleId", typeof(Guid)), ("fldSampleNumber", typeof(string))), rows);

    private static DataTable Items(params object[][] rows) => Rows(
        Table(("fldSampleReceiptId", typeof(Guid)), ("fldLabcode", typeof(string)), ("fldSampleTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)),
            ("fldDistributionParticipantId", typeof(Guid)), ("fldAssessmentDate", typeof(DateTime))),
        rows);

    private static void RespondToScalarMultiLine(FakeDbConnection connection, string sql, object? value)
    {
        connection.RespondToScalar(sql.ReplaceLineEndings("\n"), value);
        connection.RespondToScalar(sql.ReplaceLineEndings("\r\n"), value);
    }

    // ---- Years ----------------------------------------------------------------------------------

    [Fact]
    public async Task GetYearsAsync_ReturnsTheYearsInProcedureOrderForTheAssessor()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(YearsSql, Years([2019], [2020], [2021]));

        var years = await repository.GetYearsAsync(Assessor);

        Assert.Equal([2019, 2020, 2021], years);
        Assert.Equal(Assessor, Assert.Single(connection.ExecutedCommands).ParameterValue("AssessorId"));
    }

    [Fact]
    public async Task GetYearsAsync_NoDistributions_IsEmpty()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(YearsSql, Years());

        Assert.Empty(await repository.GetYearsAsync(Assessor));
    }

    // ---- Months ---------------------------------------------------------------------------------

    [Fact]
    public async Task GetMonthsAsync_GroupsDistributionsUnderTheirMonthAndTestsUnderTheirDistribution()
    {
        var (repository, connection) = CreateRepository();
        var submitted = new DateTime(2021, 3, 17, 13, 17, 39);
        connection.RespondToQuery(MonthsSql, Sets(
            Months([Feb, 2021, 2], [Jan, 2021, 1]),
            Distributions(
                [2021, Feb, SchemeA, "Cellular Pathology Technique (CPT)", "PT0166", "14468", "/A", "HI"],
                [2021, Feb, SchemeB, "Immunohistochemistry (IHC)", "PT0167", "14469", string.Empty, "HI"],
                [2021, Jan, SchemeC, "Cellular Pathology Technique (CPT)", "PT0166", "14001", string.Empty, "HI"]),
            Tests(
                [Test1, "Giemsa", Feb, SchemeA, submitted],
                [Test2, "Haematoxylin & Eosin 1", Feb, SchemeA, DBNull.Value],
                [Test3, "E-cadherin", Feb, SchemeB, DBNull.Value])));

        var months = await repository.GetMonthsAsync(Assessor, 2021);

        Assert.Equal([(2021, 2), (2021, 1)], months.Select(m => (m.YearId, m.MonthId)));
        var february = months[0];
        Assert.Equal([SchemeA, SchemeB], february.Distributions.Select(d => d.MonthlyDistributionSchemeId));
        var first = february.Distributions[0];
        Assert.Equal(("Cellular Pathology Technique (CPT)", "PT0166", "14468", "/A", "HI"),
            (first.SchemeName, first.SchemeIdentifier, first.DistributionReference, first.DistributionReferenceSuffix, first.ScheduleCode));
        Assert.Equal([Test1, Test2], first.Tests.Select(t => t.TestId));
        Assert.Equal(("Giemsa", (DateTime?)submitted), (first.Tests[0].TestType, first.Tests[0].AssessmentSubmitted));
        Assert.Null(first.Tests[1].AssessmentSubmitted);
        Assert.Equal([Test3], february.Distributions[1].Tests.Select(t => t.TestId));
        Assert.Equal([SchemeC], months[1].Distributions.Select(d => d.MonthlyDistributionSchemeId));
        Assert.Empty(months[1].Distributions[0].Tests);
    }

    [Fact]
    public async Task GetMonthsAsync_SendsTheYearAndTheAssessorToTheProcedure()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(MonthsSql, Sets(Months(), Distributions(), Tests()));

        Assert.Empty(await repository.GetMonthsAsync(Assessor, 2020));

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal((2020, Assessor), (command.ParameterValue("YearId"), command.ParameterValue("AssessorId")));
    }

    [Fact]
    public async Task GetMonthsAsync_NullColumns_MapToEmptyText()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(MonthsSql, Sets(
            Months([Feb, 2021, 2]),
            Distributions([2021, Feb, SchemeA, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value]),
            Tests([Test1, DBNull.Value, Feb, SchemeA, DBNull.Value])));

        var distribution = Assert.Single(Assert.Single(await repository.GetMonthsAsync(Assessor, 2021)).Distributions);

        Assert.Equal((string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            (distribution.SchemeName, distribution.SchemeIdentifier, distribution.DistributionReference, distribution.DistributionReferenceSuffix, distribution.ScheduleCode));
        Assert.Equal(string.Empty, Assert.Single(distribution.Tests).TestType);
    }

    // ---- Access ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, AssessorAccess.Granted)]
    [InlineData(0, AssessorAccess.Forbidden)]
    public async Task GetTestAccessAsync_IsGrantedOnlyToOneOfTheSchemesAssessors(int isAssessor, AssessorAccess expected)
    {
        var (repository, connection) = CreateRepository();
        RespondToScalarMultiLine(connection, AccessSql, isAssessor);

        Assert.Equal(expected, await repository.GetTestAccessAsync(Test1, Assessor));

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal((Test1, Assessor), (command.ParameterValue("TestId"), command.ParameterValue("AssessorId")));
    }

    [Fact]
    public async Task GetTestAccessAsync_UnknownTest_IsNotFound()
    {
        var (repository, connection) = CreateRepository();
        RespondToScalarMultiLine(connection, AccessSql, null);

        Assert.Equal(AssessorAccess.NotFound, await repository.GetTestAccessAsync(Test1, Assessor));
    }

    // ---- Item list ------------------------------------------------------------------------------

    [Fact]
    public async Task GetItemListAsync_BuildsTheSamplesWithTheirParticipantRows()
    {
        var (repository, connection) = CreateRepository();
        var submitted = new DateTime(2021, 3, 17, 13, 17, 39);
        var entered = new DateTime(2021, 3, 15, 16, 1, 19);
        connection.RespondToQuery(ItemsSql, Sets(
            ItemHeader([submitted, "Mary Dexter", "Cellular Pathology Technique (CPT)", "PT0166", "14468", "/A", "HI", "Alcian blue / PAS", 2021, 2]),
            ItemSamples([Sample1, "21/7140"], [Sample2, "21/7141"]),
            Items(
                [Guid.NewGuid(), "1126", SampleTest1, Sample1, ParticipantA, entered],
                [Guid.NewGuid(), "1385", SampleTest1, Sample1, ParticipantB, DBNull.Value],
                [Guid.NewGuid(), "1126", SampleTest1, Sample2, ParticipantA, DBNull.Value])));

        var list = await repository.GetItemListAsync(Test1);

        Assert.NotNull(list);
        Assert.Equal((2021, 2, "Alcian blue / PAS", "Mary Dexter", (DateTime?)submitted), (list.YearId, list.MonthId, list.TestName, list.AssessorName, list.AssessmentSubmitted));
        Assert.Equal(("Cellular Pathology Technique (CPT)", "PT0166", "14468", "/A", "HI"),
            (list.SchemeName, list.SchemeIdentifier, list.DistributionReference, list.DistributionReferenceSuffix, list.ScheduleCode));
        Assert.Equal(["21/7140", "21/7141"], list.Samples.Select(s => s.SampleNumber));
        var first = list.Samples[0];
        Assert.Equal(["1126", "1385"], first.Items.Select(i => i.LabCode));
        Assert.Equal((SampleTest1, ParticipantA, (DateTime?)entered), (first.Items[0].SampleTestId, first.Items[0].ParticipantId, first.Items[0].AssessmentDate));
        Assert.Null(first.Items[1].AssessmentDate);
        Assert.Equal(["1126"], list.Samples[1].Items.Select(i => i.LabCode));
        Assert.Equal(Test1, Assert.Single(connection.ExecutedCommands).ParameterValue("TestId"));
    }

    [Fact]
    public async Task GetItemListAsync_NotYetSubmitted_HasNoSubmissionDate()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(ItemsSql, Sets(
            ItemHeader([DBNull.Value, DBNull.Value, "Scheme", "PT0166", "1", string.Empty, "HI", "Test", 2021, 2]),
            ItemSamples(),
            Items()));

        var list = await repository.GetItemListAsync(Test1);

        Assert.Null(list!.AssessmentSubmitted);
        Assert.Equal(string.Empty, list.AssessorName);
        Assert.Empty(list.Samples);
    }

    [Fact]
    public async Task GetItemListAsync_NoHeader_IsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(ItemsSql, Sets(ItemHeader(), ItemSamples(), Items()));

        Assert.Null(await repository.GetItemListAsync(Test1));
    }

    [Fact]
    public async Task GetItemListAsync_NullColumns_MapToEmptyText()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(ItemsSql, Sets(
            ItemHeader([DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 2021, 2]),
            ItemSamples([Sample1, DBNull.Value]),
            Items([Guid.NewGuid(), DBNull.Value, SampleTest1, Sample1, ParticipantA, DBNull.Value])));

        var list = (await repository.GetItemListAsync(Test1))!;

        Assert.Equal((string.Empty, string.Empty, string.Empty), (list.SchemeName, list.TestName, list.ScheduleCode));
        var sample = Assert.Single(list.Samples);
        Assert.Equal(string.Empty, sample.SampleNumber);
        Assert.Equal(string.Empty, Assert.Single(sample.Items).LabCode);
    }

    // ---- Submit ---------------------------------------------------------------------------------

    private const string SubmitSql = "EXEC dbo.sppSubmitAssessment @TestId = @TestId, @AssessmentSubmitted = @AssessmentSubmitted, @AssessorId = @AssessorId";

    private static (AssessorRepository Repository, FakeDbConnection Connection) SubmittingRepository(int isAssessor = 1, object? alreadySubmitted = null, bool answerTheSubmit = true, bool answerTheLog = true)
    {
        var (repository, connection) = CreateRepository();
        RespondToScalarMultiLine(connection, AccessSql, isAssessor);
        connection.RespondToScalar(SubmittedSql, alreadySubmitted);
        if (answerTheSubmit)
        {
            connection.RespondToNonQuery(SubmitSql, 1);
        }

        if (answerTheLog)
        {
            connection.RespondToNonQuery(LogSql, 1);
        }

        return (repository, connection);
    }

    [Fact]
    public async Task SubmitAsync_StampsTheTestAndLogsTheSubmissionInOneTransaction()
    {
        var (repository, connection) = SubmittingRepository();

        Assert.Equal(AssessorSubmitOutcome.Submitted, await repository.SubmitAsync(Test1, Assessor, When));

        var submit = Assert.Single(connection.ExecutedCommands, c => c.CommandText == SubmitSql);
        Assert.Equal((Test1, When, Assessor), (submit.ParameterValue("TestId"), submit.ParameterValue("AssessmentSubmitted"), submit.ParameterValue("AssessorId")));
        var log = Assert.Single(connection.ExecutedCommands, c => c.CommandText == LogSql);
        Assert.Equal(Assessor.ToString(), log.ParameterValue("UserId"));
        Assert.Equal($"Assessor|SubmitAssessment|DistributionTest|{Test1}|Assessment submitted for the test", log.ParameterValue("Description"));
        Assert.True(connection.LastTransaction!.Committed);
        Assert.False(connection.LastTransaction.RolledBack);
    }

    [Fact]
    public async Task SubmitAsync_ChecksTheAssignmentThenLocksTheTestBeforeWriting()
    {
        var (repository, connection) = SubmittingRepository();

        await repository.SubmitAsync(Test1, Assessor, When);

        var texts = connection.ExecutedCommands.Select(c => c.CommandText).ToList();
        Assert.Equal(3 + 1, texts.Count);
        Assert.Equal([SubmitSql, LogSql], texts.Skip(2));
        Assert.Equal(SubmittedSql, texts[1]);
    }

    [Fact]
    public async Task SubmitAsync_UnknownTest_IsNotFoundAndWritesNothing()
    {
        var (repository, connection) = CreateRepository();
        RespondToScalarMultiLine(connection, AccessSql, null);

        Assert.Equal(AssessorSubmitOutcome.NotFound, await repository.SubmitAsync(Test1, Assessor, When));

        Assert.Single(connection.ExecutedCommands);
        Assert.True(connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task SubmitAsync_NotTheSchemesAssessor_IsForbiddenAndWritesNothing()
    {
        var (repository, connection) = SubmittingRepository(isAssessor: 0);

        Assert.Equal(AssessorSubmitOutcome.Forbidden, await repository.SubmitAsync(Test1, Assessor, When));

        Assert.Single(connection.ExecutedCommands);
        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    [Fact]
    public async Task SubmitAsync_AlreadySubmitted_ChangesNothingSoTheFirstSubmissionStands()
    {
        var (repository, connection) = SubmittingRepository(alreadySubmitted: new DateTime(2026, 10, 27, 14, 0, 0));

        Assert.Equal(AssessorSubmitOutcome.AlreadySubmitted, await repository.SubmitAsync(Test1, Assessor, When));

        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == SubmitSql || c.CommandText == LogSql);
        Assert.True(connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task SubmitAsync_TheSubmissionFails_RollsBackAndThrows()
    {
        var (repository, connection) = SubmittingRepository(answerTheSubmit: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SubmitAsync(Test1, Assessor, When));

        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    [Fact]
    public async Task SubmitAsync_TheLogWriteFails_RollsTheSubmissionBack()
    {
        var (repository, connection) = SubmittingRepository(answerTheLog: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SubmitAsync(Test1, Assessor, When));

        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    // ---- Assessment entry -----------------------------------------------------------------------

    private const string EntrySql = "EXEC dbo.spgAssessmentEntry @ParticipantId = @ParticipantId, @SampleTestId = @SampleTestId";
    private const string ResultsSql = "EXEC dbo.spgParticipantResultForAssessment @SampleTestId = @SampleTestId, @ParticipantId = @ParticipantId";
    private const string SubmittedSql = "SELECT fldAssessmentSubmitted FROM tblDistributionTest WITH (UPDLOCK, HOLDLOCK) WHERE fldTestId = @TestId";
    private const string UsernameSql = "SELECT fldUsername FROM tblUsers WHERE fldUserId = @UserId";
    private const string LogSql = "EXEC dbo.spiLog @LogId, @LogDate, @UserId, @Description";

    private const string EntryAccessSql = """
        SELECT
            CASE WHEN @AssessorId IN (s.fldAssessor1, s.fldAssessor2, s.fldAssessor3, s.fldAssessor4) THEN 1 ELSE 0 END AS fldIsAssessor,
            dt.fldTestId AS fldTestId
        FROM tblSampleReceipt sr
        INNER JOIN tblSampleTest st ON st.fldSampleTestId = sr.fldSampleTestId
        INNER JOIN tblDistributionTest dt ON dt.fldTestId = st.fldTestId
        INNER JOIN tblMonthlyDistributionScheme mds ON dt.fldDistributionSchemeId = mds.fldMonthlyDistributionSchemeId
        INNER JOIN tblScheme s ON s.fldSchemeId = mds.fldSchemeId
        WHERE sr.fldDistributionParticipantId = @ParticipantId
            AND sr.fldSampleTestId = @SampleTestId
            AND st.fldIsApplicable = 1
            AND sr.fldSubmissionDate IS NOT NULL
            AND mds.fldIsCancelled = 0
        """;

    private static readonly Guid Receipt = Guid.Parse("88888888-0000-0000-0000-000000000001");
    private static readonly Guid Morphology = Guid.Parse("99999999-0000-0000-0000-000000000001");
    private static readonly Guid Staining = Guid.Parse("99999999-0000-0000-0000-000000000002");
    private static readonly Guid Shape = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Colour = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Intensity = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly DateTime When = new(2026, 10, 27, 15, 0, 0);

    private static void RespondToQueryMultiLine(FakeDbConnection connection, string sql, DataTable table)
    {
        connection.RespondToQuery(sql.ReplaceLineEndings("\n"), table);
        connection.RespondToQuery(sql.ReplaceLineEndings("\r\n"), table);
    }

    private static DataTable EntryAccessRow(int isAssessor) =>
        Rows(Table(("fldIsAssessor", typeof(int)), ("fldTestId", typeof(Guid))), [isAssessor, Test1]);

    private static DataTable EntryAccessNone() => Table(("fldIsAssessor", typeof(int)), ("fldTestId", typeof(Guid)));

    private static DataTable EntryHeader(string comment, int score, Guid assessorId, string assessorName, object? submitted = null) => Rows(
        Table(("fldSampleReceiptId", typeof(Guid)), ("fldAssessmentComment", typeof(string)), ("fldAssessorId", typeof(Guid)), ("fldAssessorName", typeof(string)),
            ("fldScore", typeof(int)), ("fldSchemeName", typeof(string)), ("fldSchemeIdentifier", typeof(string)), ("fldDistributionReference", typeof(string)),
            ("fldDistributionReferenceSuffix", typeof(string)), ("fldScheduleCode", typeof(string)), ("fldTestName", typeof(string)), ("fldYearId", typeof(int)),
            ("fldMonthId", typeof(int)), ("fldLabcode", typeof(string)), ("fldSampleNumber", typeof(string)), ("fldTestId", typeof(Guid)), ("fldAssessmentSubmitted", typeof(DateTime))),
        [Receipt, comment, assessorId, assessorName, score, "Cellular Pathology Technique (CPT)", "PT0166", "14468", "/A", "HI", "Alcian blue / PAS", 2021, 2, "1126", "21/7140", Test1, submitted ?? DBNull.Value]);

    private static DataTable EntryHeaderNone() => Table(("fldSampleReceiptId", typeof(Guid)), ("fldTestId", typeof(Guid)));

    private static DataTable EntryCategories(params object[][] rows) =>
        Rows(Table(("fldCategoryItemId", typeof(Guid)), ("fldName", typeof(string)), ("fldParticipantId", typeof(Guid)), ("fldSampleTestId", typeof(Guid))), rows);

    private static DataTable EntryActualCategories(params object[][] rows) => Rows(
        Table(("fldActualResultsId", typeof(Guid)), ("fldSampleTestId", typeof(Guid)), ("fldCategoryItemId", typeof(Guid)), ("fldCategoryScore", typeof(int))), rows);

    private static DataTable EntryCriteria(params object[][] rows) =>
        Rows(Table(("fldCriterionItemId", typeof(Guid)), ("fldName", typeof(string)), ("fldCategoryItemId", typeof(Guid)), ("fldParticipantId", typeof(Guid))), rows);

    private static DataTable EntryActualCriteria(params object[][] rows) => Rows(
        Table(("fldActualResultsId", typeof(Guid)), ("fldCategoryItemId", typeof(Guid)), ("fldCriterionItemId", typeof(Guid)), ("fldSampleTestId", typeof(Guid))), rows);

    private static DataTable EntryResults(params object[][] rows) => Rows(
        Table(("fldId", typeof(Guid)), ("fldTestTypeName", typeof(string)), ("fldSampleNumber", typeof(string)), ("fldTestResultItemName", typeof(string)), ("fldResult", typeof(string))), rows);

    // Morphology (Shape, Colour) saved with a score of 2 and Shape ticked, and Staining (Intensity) not saved yet.
    private static DataSet EntrySets(string comment = "", int score = 0, Guid? assessorId = null, string assessorName = "", object? submitted = null) => Sets(
        EntryHeader(comment, score, assessorId ?? Guid.Empty, assessorName, submitted),
        EntryCategories([Morphology, "Morphology", ParticipantA, SampleTest1], [Staining, "Staining", ParticipantA, SampleTest1]),
        EntryActualCategories([ParticipantA, SampleTest1, Morphology, 2]),
        EntryCriteria([Shape, "Shape", Morphology, ParticipantA], [Colour, "Colour", Morphology, ParticipantA], [Intensity, "Intensity", Staining, ParticipantA]),
        EntryActualCriteria([ParticipantA, Morphology, Shape, SampleTest1]));

    private static AssessorEntrySave Save(string comment = "Good", int overall = 3, int morphologyScore = 2, Guid[]? morphology = null, int stainingScore = 4, Guid[]? staining = null) =>
        new(
            SampleTest1,
            ParticipantA,
            Assessor,
            comment,
            overall,
            [new(Morphology, morphologyScore, morphology ?? [Shape]), new(Staining, stainingScore, staining ?? [Intensity])],
            When);

    // The save of a first assessment: every command the planner issues, answered, and the reads a save makes.
    private static (AssessorRepository Repository, FakeDbConnection Connection) SavingRepository(AssessorEntrySave save, DataSet? entry = null, string? username = "jsmith", bool answerTheLog = true)
    {
        var (repository, connection) = CreateRepository();
        RespondToQueryMultiLine(connection, EntryAccessSql, EntryAccessRow(1));
        connection.RespondToScalar(SubmittedSql, (object?)null);
        connection.RespondToScalar(UsernameSql, username);
        var current = entry ?? EntrySets();
        connection.RespondToQuery(EntrySql, current);
        if (answerTheLog)
        {
            connection.RespondToNonQuery(LogSql, 1);
        }

        foreach (var text in AllCommandTexts(save))
        {
            connection.RespondToNonQuery(text, 1);
        }

        return (repository, connection);
    }

    // Every procedure call text the planner can issue for this save (the answers do not depend on the current state).
    private static IEnumerable<string> AllCommandTexts(AssessorEntrySave save)
    {
        var entry = new AssessorEntryEntity();
        foreach (var category in save.Categories)
        {
            var categoryEntity = new AssessorCategoryEntity { CategoryItemId = category.CategoryItemId, Name = "c" };
            categoryEntity.Criteria.AddRange(category.CriterionItemIds.Select(id => new AssessorCriterionEntity(id, "i", false)));
            entry.Categories.Add(categoryEntity);
        }

        var fresh = AssessorEntryPlanner.Plan(entry, save with { Comment = save.Comment + "x", OverallScore = save.OverallScore + 1 }, "n");
        return fresh.Commands.Select(c => c.CommandText)
            .Concat(
            [
                "EXEC dbo.spuActualCategoryItemResult @ActualResultsId = @ActualResultsId, @CategoryItemId = @CategoryItemId, @SampleTestId = @SampleTestId, @CategoryScore = @CategoryScore",
                "EXEC dbo.spdActualCriterionItemResult @ActualResultsId = @ActualResultsId, @CriterionItemId = @CriterionItemId, @SampleTestId = @SampleTestId",
                "EXEC dbo.spiActualCriterionItemResult @ActualResultsId = @ActualResultsId, @CriterionItemId = @CriterionItemId, @SampleTestId = @SampleTestId",
            ]);
    }

    [Theory]
    [InlineData(1, AssessorAccess.Granted)]
    [InlineData(0, AssessorAccess.Forbidden)]
    public async Task GetEntryAccessAsync_IsGrantedOnlyToOneOfTheSchemesAssessors(int isAssessor, AssessorAccess expected)
    {
        var (repository, connection) = CreateRepository();
        RespondToQueryMultiLine(connection, EntryAccessSql, EntryAccessRow(isAssessor));

        Assert.Equal(expected, await repository.GetEntryAccessAsync(SampleTest1, ParticipantA, Assessor));

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal((SampleTest1, ParticipantA, Assessor), (command.ParameterValue("SampleTestId"), command.ParameterValue("ParticipantId"), command.ParameterValue("AssessorId")));
    }

    [Fact]
    public async Task GetEntryAccessAsync_NotASubmissionTheListShows_IsNotFound()
    {
        var (repository, connection) = CreateRepository();
        RespondToQueryMultiLine(connection, EntryAccessSql, EntryAccessNone());

        Assert.Equal(AssessorAccess.NotFound, await repository.GetEntryAccessAsync(SampleTest1, ParticipantA, Assessor));
    }

    [Fact]
    public async Task GetEntryAsync_BuildsTheCategoriesWithTheirSavedScoresAndTickedCriteria()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(EntrySql, EntrySets(comment: "Good", score: 3, assessorId: Assessor, assessorName: "jsmith"));
        connection.RespondToQuery(ResultsSql, EntryResults([Guid.NewGuid(), "Giemsa", "21/7140", "Isolate", "Positive"], [Guid.NewGuid(), "Giemsa", "21/7140", "Count", "12"]));

        var entry = (await repository.GetEntryAsync(SampleTest1, ParticipantA))!;

        Assert.Equal((Receipt, Test1, "1126", "21/7140", "Alcian blue / PAS"), (entry.SampleReceiptId, entry.TestId, entry.LabCode, entry.SampleNumber, entry.TestName));
        Assert.Equal((2021, 2, "14468", "/A", "HI", "PT0166", "Cellular Pathology Technique (CPT)"),
            (entry.YearId, entry.MonthId, entry.DistributionReference, entry.DistributionReferenceSuffix, entry.ScheduleCode, entry.SchemeIdentifier, entry.SchemeName));
        Assert.Equal(("Good", 3, Assessor, "jsmith"), (entry.Comment, entry.Score, entry.AssessorId, entry.AssessorName));
        Assert.Null(entry.AssessmentSubmitted);
        Assert.Equal(["Morphology", "Staining"], entry.Categories.Select(c => c.Name));
        Assert.Equal([2, (int?)null], entry.Categories.Select(c => c.ActualScore));
        Assert.Equal([Shape, Colour], entry.Categories[0].Criteria.Select(c => c.CriterionItemId));
        Assert.Equal([true, false], entry.Categories[0].Criteria.Select(c => c.IsSelected));
        Assert.Equal([false], entry.Categories[1].Criteria.Select(c => c.IsSelected));
        Assert.Equal([("Isolate", "Positive"), ("Count", "12")], entry.ResultItems.Select(r => (r.Name, r.Result)));
        Assert.Equal([EntrySql, ResultsSql], connection.ExecutedCommands.Select(c => c.CommandText));
    }

    [Fact]
    public async Task GetEntryAsync_Submitted_CarriesTheSubmissionDate()
    {
        var (repository, connection) = CreateRepository();
        var submitted = new DateTime(2021, 3, 17, 13, 17, 39);
        connection.RespondToQuery(EntrySql, EntrySets(submitted: submitted));
        connection.RespondToQuery(ResultsSql, EntryResults());

        Assert.Equal(submitted, (await repository.GetEntryAsync(SampleTest1, ParticipantA))!.AssessmentSubmitted);
    }

    [Fact]
    public async Task GetEntryAsync_NoSuchSubmission_IsNullAndReadsNoResults()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(EntrySql, Sets(EntryHeaderNone(), EntryCategories(), EntryActualCategories(), EntryCriteria(), EntryActualCriteria()));

        Assert.Null(await repository.GetEntryAsync(SampleTest1, ParticipantA));
        Assert.Equal(EntrySql, Assert.Single(connection.ExecutedCommands).CommandText);
    }

    [Fact]
    public async Task GetEntryAsync_NullColumns_MapToDefaults()
    {
        var (repository, connection) = CreateRepository();
        var header = EntryHeader(string.Empty, 0, Guid.Empty, string.Empty);
        header.Rows[0]["fldAssessmentComment"] = DBNull.Value;
        header.Rows[0]["fldAssessorId"] = DBNull.Value;
        header.Rows[0]["fldAssessorName"] = DBNull.Value;
        header.Rows[0]["fldScore"] = DBNull.Value;
        connection.RespondToQuery(EntrySql, Sets(header, EntryCategories([Morphology, DBNull.Value, ParticipantA, SampleTest1]), EntryActualCategories([ParticipantA, SampleTest1, Morphology, DBNull.Value]), EntryCriteria([Shape, DBNull.Value, Morphology, ParticipantA]), EntryActualCriteria()));
        connection.RespondToQuery(ResultsSql, EntryResults([Guid.NewGuid(), DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value]));

        var entry = (await repository.GetEntryAsync(SampleTest1, ParticipantA))!;

        Assert.Equal((string.Empty, 0, Guid.Empty, string.Empty), (entry.Comment, entry.Score, entry.AssessorId, entry.AssessorName));
        Assert.Equal(("", 0), (entry.Categories[0].Name, entry.Categories[0].ActualScore ?? -1));
        Assert.Equal(string.Empty, entry.Categories[0].Criteria[0].Name);
        Assert.Equal((string.Empty, string.Empty), (entry.ResultItems[0].Name, entry.ResultItems[0].Result));
    }

    [Fact]
    public async Task SaveEntryAsync_FirstSave_WritesTheEntryAndTheCategoriesInOneTransactionAndLogsTheCategoryChanges()
    {
        var save = Save(comment: "Good", overall: 3, morphology: [Colour], morphologyScore: 3, staining: [Intensity], stainingScore: 5);
        var (repository, connection) = SavingRepository(save);

        var outcome = await repository.SaveEntryAsync(save);

        Assert.Equal(AssessorSaveOutcome.Saved, outcome);
        Assert.True(connection.LastTransaction!.Committed);
        Assert.False(connection.LastTransaction.RolledBack);
        var writes = connection.ExecutedCommands
            .Select(c => c.CommandText)
            .Where(text => text.StartsWith("EXEC dbo.sp", StringComparison.Ordinal) && !text.StartsWith("EXEC dbo.spg", StringComparison.Ordinal) && !text.StartsWith("EXEC dbo.spiLog", StringComparison.Ordinal))
            .Select(text => text.Split(' ')[1]);
        Assert.Equal(
            ["dbo.spiAuditAssessmentItem", "dbo.spuAssessmentEntry", "dbo.spuActualCategoryItemResult", "dbo.spiActualCriterionItemResult", "dbo.spdActualCriterionItemResult", "dbo.spiActualCategoryItemResult", "dbo.spiActualCriterionItemResult"],
            writes);
    }

    [Fact]
    public async Task SaveEntryAsync_TheEntryRow_IsStampedWithTheUsernameTheAssessorAndTheDate()
    {
        var save = Save();
        var (repository, connection) = SavingRepository(save);

        await repository.SaveEntryAsync(save);

        var entry = Assert.Single(connection.ExecutedCommands, c => c.CommandText.StartsWith("EXEC dbo.spuAssessmentEntry", StringComparison.Ordinal));
        Assert.Equal((ParticipantA, SampleTest1, 3, Assessor, "jsmith", When, "Good"),
            (entry.ParameterValue("ParticipantId"), entry.ParameterValue("SampleTestId"), entry.ParameterValue("Score"), entry.ParameterValue("AssessorId"), entry.ParameterValue("AssessorName"), entry.ParameterValue("AssessmentDate"), entry.ParameterValue("AssessmentComment")));
        var audit = Assert.Single(connection.ExecutedCommands, c => c.CommandText.StartsWith("EXEC dbo.spiAuditAssessmentItem", StringComparison.Ordinal));
        Assert.Equal((Receipt, "jsmith", "Assessment Entry", true, true), (audit.ParameterValue("SampleReceiptId"), audit.ParameterValue("AuditWho"), audit.ParameterValue("AuditWhy"), audit.ParameterValue("AssessmentCommentUpdate"), audit.ParameterValue("ScoreUpdate")));
    }

    [Fact]
    public async Task SaveEntryAsync_TheCategoryChanges_AreLoggedToTheExistingLogWithoutTheComment()
    {
        var save = Save(comment: "Private words", morphology: [Colour], morphologyScore: 3);
        var (repository, connection) = SavingRepository(save);

        await repository.SaveEntryAsync(save);

        var log = Assert.Single(connection.ExecutedCommands, c => c.CommandText == LogSql);
        Assert.Equal(Assessor.ToString(), log.ParameterValue("UserId"));
        var description = Assert.IsType<string>(log.ParameterValue("Description"));
        Assert.StartsWith($"Assessor|SaveAssessmentEntry|AssessmentEntry|{Receipt}|Morphology: score 2->3, criteria added Colour, criteria removed Shape", description, StringComparison.Ordinal);
        Assert.DoesNotContain("Private words", description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveEntryAsync_NothingChanged_WritesAndLogsNothingButStillSucceeds()
    {
        var save = Save(comment: "Good", overall: 3, stainingScore: 4);
        var saved = Sets(
            EntryHeader("Good", 3, Assessor, "jsmith"),
            EntryCategories([Morphology, "Morphology", ParticipantA, SampleTest1], [Staining, "Staining", ParticipantA, SampleTest1]),
            EntryActualCategories([ParticipantA, SampleTest1, Morphology, 2], [ParticipantA, SampleTest1, Staining, 4]),
            EntryCriteria([Shape, "Shape", Morphology, ParticipantA], [Colour, "Colour", Morphology, ParticipantA], [Intensity, "Intensity", Staining, ParticipantA]),
            EntryActualCriteria([ParticipantA, Morphology, Shape, SampleTest1], [ParticipantA, Staining, Intensity, SampleTest1]));
        var (repository, connection) = SavingRepository(save, saved);

        Assert.Equal(AssessorSaveOutcome.Saved, await repository.SaveEntryAsync(save));

        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText.StartsWith("EXEC dbo.sp", StringComparison.Ordinal) && !c.CommandText.StartsWith("EXEC dbo.spg", StringComparison.Ordinal));
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == LogSql);
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task SaveEntryAsync_OnlyTheCommentChanged_AuditsItButLogsNothingExtra()
    {
        var save = Save(comment: "New", overall: 3, stainingScore: 4);
        var saved = Sets(
            EntryHeader("Old", 3, Assessor, "jsmith"),
            EntryCategories([Morphology, "Morphology", ParticipantA, SampleTest1], [Staining, "Staining", ParticipantA, SampleTest1]),
            EntryActualCategories([ParticipantA, SampleTest1, Morphology, 2], [ParticipantA, SampleTest1, Staining, 4]),
            EntryCriteria([Shape, "Shape", Morphology, ParticipantA], [Colour, "Colour", Morphology, ParticipantA], [Intensity, "Intensity", Staining, ParticipantA]),
            EntryActualCriteria([ParticipantA, Morphology, Shape, SampleTest1], [ParticipantA, Staining, Intensity, SampleTest1]));
        var (repository, connection) = SavingRepository(save, saved);

        await repository.SaveEntryAsync(save);

        Assert.Single(connection.ExecutedCommands, c => c.CommandText.StartsWith("EXEC dbo.spiAuditAssessmentItem", StringComparison.Ordinal));
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == LogSql);
    }

    [Fact]
    public async Task SaveEntryAsync_AnAssessorWhoCannotBeNamed_IsSavedAsGuest()
    {
        var save = Save();
        var (repository, connection) = SavingRepository(save, username: null);

        await repository.SaveEntryAsync(save);

        Assert.Equal("Guest", Assert.Single(connection.ExecutedCommands, c => c.CommandText.StartsWith("EXEC dbo.spuAssessmentEntry", StringComparison.Ordinal)).ParameterValue("AssessorName"));
    }

    [Fact]
    public async Task SaveEntryAsync_NotTheSchemesAssessor_IsForbiddenAndWritesNothing()
    {
        var (repository, connection) = CreateRepository();
        RespondToQueryMultiLine(connection, EntryAccessSql, EntryAccessRow(0));

        Assert.Equal(AssessorSaveOutcome.Forbidden, await repository.SaveEntryAsync(Save()));

        Assert.Single(connection.ExecutedCommands);
        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    [Fact]
    public async Task SaveEntryAsync_NoSuchSubmission_IsNotFoundAndRollsBack()
    {
        var (repository, connection) = CreateRepository();
        RespondToQueryMultiLine(connection, EntryAccessSql, EntryAccessNone());

        Assert.Equal(AssessorSaveOutcome.NotFound, await repository.SaveEntryAsync(Save()));

        Assert.True(connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task SaveEntryAsync_TheTestWasSubmittedMeanwhile_IsRefusedWithoutWriting()
    {
        var (repository, connection) = CreateRepository();
        RespondToQueryMultiLine(connection, EntryAccessSql, EntryAccessRow(1));
        connection.RespondToScalar(SubmittedSql, new DateTime(2026, 10, 27, 14, 0, 0));

        Assert.Equal(AssessorSaveOutcome.Submitted, await repository.SaveEntryAsync(Save()));

        Assert.Equal(2, connection.ExecutedCommands.Count);
        Assert.True(connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task SaveEntryAsync_TheSubmissionDisappears_IsNotFound()
    {
        var (repository, connection) = CreateRepository();
        RespondToQueryMultiLine(connection, EntryAccessSql, EntryAccessRow(1));
        connection.RespondToScalar(SubmittedSql, (object?)null);
        connection.RespondToQuery(EntrySql, Sets(EntryHeaderNone(), EntryCategories(), EntryActualCategories(), EntryCriteria(), EntryActualCriteria()));

        Assert.Equal(AssessorSaveOutcome.NotFound, await repository.SaveEntryAsync(Save()));

        Assert.True(connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task SaveEntryAsync_AWriteFails_RollsEverythingBackAndThrows()
    {
        var (repository, connection) = CreateRepository();
        RespondToQueryMultiLine(connection, EntryAccessSql, EntryAccessRow(1));
        connection.RespondToScalar(SubmittedSql, (object?)null);
        connection.RespondToScalar(UsernameSql, "jsmith");
        connection.RespondToQuery(EntrySql, EntrySets());

        // None of the writes has a configured response, so the fake throws as the first one fails.
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveEntryAsync(Save()));

        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    [Fact]
    public async Task SaveEntryAsync_TheLogWriteFails_RollsTheWholeSaveBack()
    {
        var save = Save(morphology: [Colour], morphologyScore: 3);
        var (repository, connection) = SavingRepository(save, answerTheLog: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveEntryAsync(save));

        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    [Fact]
    public async Task SaveEntryAsync_NullSave_Throws()
    {
        var (repository, _) = CreateRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.SaveEntryAsync(null!));
    }
}
