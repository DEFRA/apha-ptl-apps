using System.Data;
using PTL.Core.Distribution;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// spgaMonthlyDistributionInfoForResultsSignOff - legacy Results Sign-off MonthlyDistributionInfoCollection.FetchCollection -
// and the inline query for the Test Consultant and Assessor names.
public class ResultsSignOffRepositoryTests
{
    public ResultsSignOffRepositoryTests() => DapperColumnMappings.Register();

    private const string QueueSql = "EXEC dbo.spgaMonthlyDistributionInfoForResultsSignOff";

    private const string NamesSql = """
        SELECT
            mds.fldMonthlyDistributionSchemeId AS fldMonthlyDistributionSchemeId,
            dbo.fnGetTestConsultantNameById(s.fldTestConsultant1) AS fldTestConsultant1,
            dbo.fnGetTestConsultantNameById(s.fldTestConsultant2) AS fldTestConsultant2,
            dbo.fnGetTestConsultantNameById(s.fldTestConsultant3) AS fldTestConsultant3,
            dbo.fnGetAssessorNameById(s.fldAssessor1) AS fldAssessor1,
            dbo.fnGetAssessorNameById(s.fldAssessor2) AS fldAssessor2,
            dbo.fnGetAssessorNameById(s.fldAssessor3) AS fldAssessor3,
            dbo.fnGetAssessorNameById(s.fldAssessor4) AS fldAssessor4
        FROM tblMonthlyDistributionScheme mds
        INNER JOIN tblScheme s ON mds.fldSchemeId = s.fldSchemeId
        WHERE mds.fldIsReadyForResultsSignOff IS NOT NULL
            AND mds.fldIsCancelled = 0
            AND EXISTS (
                SELECT 1 FROM tblDistributionTabulation t
                WHERE t.fldMonthlyDistributionSchemeId = mds.fldMonthlyDistributionSchemeId
                    AND t.fldPublishState = 0)
        """;

    private static readonly Guid May = Guid.Parse("33333333-0000-0000-0000-000000000005");
    private static readonly Guid June = Guid.Parse("33333333-0000-0000-0000-000000000006");
    private static readonly Guid SchemeA = Guid.Parse("22222222-0000-0000-0000-00000000000a");
    private static readonly Guid SchemeB = Guid.Parse("22222222-0000-0000-0000-00000000000b");
    private static readonly Guid TabulationA1 = Guid.Parse("44444444-0000-0000-0000-0000000000a1");
    private static readonly Guid TabulationA2 = Guid.Parse("44444444-0000-0000-0000-0000000000a2");
    private static readonly Guid TabulationB1 = Guid.Parse("44444444-0000-0000-0000-0000000000b1");

    private static (ResultsSignOffRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new ResultsSignOffRepository(new FakeDbConnectionFactory(connection)), connection);
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

    private static DataTable Months(params object[][] rows) =>
        Rows(Table(("fldMonthlyDistributionId", typeof(Guid)), ("fldYearId", typeof(int)), ("fldMonthId", typeof(int))), rows);

    private static DataTable Schemes(params object[][] rows) => Rows(
        Table(("fldMonthlyDistributionId", typeof(Guid)), ("fldMonthlyDistributionSchemeId", typeof(Guid)), ("fldSchemeName", typeof(string)),
            ("fldSchemeIdentifier", typeof(string)), ("fldDistributionReference", typeof(string)), ("fldDistributionReferenceSuffix", typeof(string)),
            ("fldScheduleCode", typeof(string)), ("fldResultsIssueTargetDate", typeof(DateTime))),
        rows);

    private static DataTable Tabulations(params object[][] rows) => Rows(
        Table(("fldMonthlyDistributionId", typeof(Guid)), ("fldMonthlyDistributionSchemeId", typeof(Guid)), ("fldDistributionTabulationId", typeof(Guid)),
            ("fldName", typeof(string)), ("fldPublishState", typeof(int)), ("fldPublishCount", typeof(int)), ("fldSingleParticipantTabulation", typeof(bool)),
            ("fldAvailableToParticipants", typeof(bool)), ("fldAvailableToViewers", typeof(bool))),
        rows);

    private static DataTable Names(params object[][] rows) => Rows(
        Table(("fldMonthlyDistributionSchemeId", typeof(Guid)), ("fldTestConsultant1", typeof(string)), ("fldTestConsultant2", typeof(string)),
            ("fldTestConsultant3", typeof(string)), ("fldAssessor1", typeof(string)), ("fldAssessor2", typeof(string)),
            ("fldAssessor3", typeof(string)), ("fldAssessor4", typeof(string))),
        rows);

    [Fact]
    public async Task GetQueueAsync_NothingAwaitingSignOff_ReturnsNothingWithoutReadingNames()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(QueueSql, Sets(Months(), Schemes(), Tabulations()));

        var queue = await repository.GetQueueAsync();

        Assert.Empty(queue);
        Assert.Equal(QueueSql, Assert.Single(connection.ExecutedCommands).CommandText);
    }

    [Fact]
    public async Task GetQueueAsync_GroupsTabulationsUnderTheirSchemeAndMonthKeepingTheProcedureOrder()
    {
        var (repository, connection) = CreateRepository();
        var due = new DateTime(2026, 7, 21);
        connection.RespondToQuery(QueueSql, Sets(
            Months([May, 2026, 5], [June, 2026, 6]),
            Schemes(
                [May, SchemeA, "Brucella", "BT", "17001", "/BA", "PT0001", new DateTime(2026, 6, 1)],
                [June, SchemeB, "Pasteurella", "PM", "17208", "/BA", "PT0074", due]),
            Tabulations(
                [May, SchemeA, TabulationA1, "FINAL REPORT", 0, 2, true, true, true],
                [May, SchemeA, TabulationA2, "INTERIM", 0, 0, false, true, false],
                [June, SchemeB, TabulationB1, "FINAL REPORT", 0, 0, false, false, true])));
        RespondToMultiLine(connection, NamesSql, Names(
            [SchemeA, "Jane Smith", "John Doe", DBNull.Value, "Ann Assessor", DBNull.Value, DBNull.Value, DBNull.Value],
            [SchemeB, DBNull.Value, DBNull.Value, DBNull.Value, "Ann Assessor", "Bob Assessor", DBNull.Value, "Cy Assessor"]));

        var queue = await repository.GetQueueAsync();

        Assert.Equal([(2026, 5), (2026, 6)], queue.Select(m => (m.YearId, m.MonthId)));
        var may = Assert.Single(queue[0].Schemes);
        Assert.Equal((SchemeA, "Brucella", "BT", "17001", "/BA", "PT0001", new DateTime(2026, 6, 1)),
            (may.MonthlyDistributionSchemeId, may.SchemeName, may.SchemeIdentifier, may.DistributionReference, may.DistributionReferenceSuffix, may.ScheduleCode, may.TargetDate));
        Assert.Equal(("Jane Smith, John Doe", "Ann Assessor"), (may.TestConsultants, may.Assessors));
        Assert.Equal([TabulationA1, TabulationA2], may.Tabulations.Select(t => t.DistributionTabulationId));
        Assert.Equal(("FINAL REPORT", 2, true, true, true),
            (may.Tabulations[0].Name, may.Tabulations[0].PublishCount, may.Tabulations[0].SingleParticipantTabulation, may.Tabulations[0].AvailableToParticipants, may.Tabulations[0].AvailableToViewers));
        Assert.Equal((0, false, true, false),
            (may.Tabulations[1].PublishCount, may.Tabulations[1].SingleParticipantTabulation, may.Tabulations[1].AvailableToParticipants, may.Tabulations[1].AvailableToViewers));

        var june = Assert.Single(queue[1].Schemes);
        Assert.Equal((string.Empty, "Ann Assessor, Bob Assessor, Cy Assessor"), (june.TestConsultants, june.Assessors));
        Assert.Equal(2, connection.ExecutedCommands.Count);
    }

    [Fact]
    public async Task GetQueueAsync_NullColumnsAndNoNamesRow_MapToDefaults()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(QueueSql, Sets(
            Months([June, 2026, 6]),
            Schemes([June, SchemeA, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value]),
            Tabulations([June, SchemeA, TabulationA1, DBNull.Value, 0, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value])));
        RespondToMultiLine(connection, NamesSql, Names());

        var scheme = Assert.Single(Assert.Single(await repository.GetQueueAsync()).Schemes);

        Assert.Equal((string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, default(DateTime), string.Empty, string.Empty),
            (scheme.SchemeName, scheme.SchemeIdentifier, scheme.DistributionReference, scheme.DistributionReferenceSuffix, scheme.ScheduleCode, scheme.TargetDate, scheme.TestConsultants, scheme.Assessors));
        var tabulation = Assert.Single(scheme.Tabulations);
        Assert.Equal((string.Empty, 0, false, false, false),
            (tabulation.Name, tabulation.PublishCount, tabulation.SingleParticipantTabulation, tabulation.AvailableToParticipants, tabulation.AvailableToViewers));
    }

    [Fact]
    public async Task GetQueueAsync_SchemeOfAnUnknownMonth_IsNotListed()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(QueueSql, Sets(
            Months([June, 2026, 6]),
            Schemes([May, SchemeA, "Orphan", "OR", "1", "a", "S", new DateTime(2026, 1, 1)]),
            Tabulations()));
        RespondToMultiLine(connection, NamesSql, Names());

        var month = Assert.Single(await repository.GetQueueAsync());

        Assert.Empty(month.Schemes);
    }

    // ---- Reject ---------------------------------------------------------------------------------

    private const string StateSql = "SELECT fldPublishState FROM tblDistributionTabulation WITH (UPDLOCK, HOLDLOCK) WHERE fldDistributionTabulationId = @DistributionTabulationId";

    private const string SetStateSql = "EXEC dbo.spuTabulationForSignOff @DistributionTabulationId = @DistributionTabulationId, @PublishState = @PublishState";

    [Fact]
    public async Task RejectAsync_AwaitingSignOff_MarksItNotAuthorisedAndCommits()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(StateSql, 0);
        connection.RespondToNonQuery(SetStateSql, 1);

        var outcome = await repository.RejectAsync(TabulationA1);

        Assert.Equal(PTL.Core.Distribution.ResultsSignOffOutcome.Done, outcome);
        var update = Assert.Single(connection.ExecutedCommands, c => c.CommandText == SetStateSql);
        Assert.Equal((TabulationA1, 2), (update.ParameterValue("DistributionTabulationId"), update.ParameterValue("PublishState")));
        Assert.True(connection.LastTransaction!.Committed);
        Assert.False(connection.LastTransaction.RolledBack);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RejectAsync_AlreadyDecided_ChangesNothingAndRollsBack(int state)
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(StateSql, state);

        var outcome = await repository.RejectAsync(TabulationA1);

        Assert.Equal(PTL.Core.Distribution.ResultsSignOffOutcome.NotAwaiting, outcome);
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == SetStateSql);
        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    [Fact]
    public async Task RejectAsync_UnknownTabulation_IsNotFoundAndRollsBack()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(StateSql, (object?)null);

        var outcome = await repository.RejectAsync(TabulationA1);

        Assert.Equal(PTL.Core.Distribution.ResultsSignOffOutcome.NotFound, outcome);
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == SetStateSql);
        Assert.True(connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task RejectAsync_UpdateFails_RollsBackAndThrows()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(StateSql, 0);

        // SetStateSql has no configured response, so the fake throws when the update runs.
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RejectAsync(TabulationA1));

        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    // ---- Authorise ------------------------------------------------------------------------------

    private const string DraftSql = "EXEC dbo.spgDistributionTabulation @DistributionTabulationId = @TabulationId";
    private const string SchemeSql = "EXEC dbo.spgMonthlyDistributionScheme @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";
    private const string IntendedResultsSql = "EXEC dbo.spgIntendedResults @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";
    private const string RecipientsSql = "EXEC dbo.sppNotificationEmails @DistributionTabulationId = @DistributionTabulationId";
    private const string QueueEmailSql = "EXEC dbo.spiNotificationEmail @NotificationEmailId = @NotificationEmailId, @PublishedTabulationId = @PublishedTabulationId, @Email = @Email, @Name = @Name";
    private const string SignatorySql = "SELECT fldFirstName, fldLastName, fldUsername FROM tblUsers WHERE fldUserId = @UserId";

    private static readonly Guid Signatory = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly DateTime Created = new(2026, 10, 26, 11, 30, 0);

    // The text of the one procedure call a tabulation with nothing else to publish makes.
    private static readonly string PublishTabulationSql = TabulationPublishPlanner.Plan(
        new TabulationPublishSource(
            new PublishHeader(TabulationA1, SchemeA, "", "", "", false, "", "", "", "", "", "", "", default, default, false, false, false, false, false, ""),
            new PublishScheme(0, 0, false, false), [], [], [], [], [], []),
        "x",
        Created).Commands.Single().CommandText;

    private static DataTable Signatories(params object[][] rows) =>
        Rows(Table(("fldFirstName", typeof(string)), ("fldLastName", typeof(string)), ("fldUsername", typeof(string))), rows);

    private static DataTable Header(Guid tabulationId, Guid schemeId) =>
        Rows(Table(("fldDistributionTabulationId", typeof(Guid)), ("fldMonthlyDistributionSchemeId", typeof(Guid)), ("fldTabulationName", typeof(string))),
            [tabulationId, schemeId, "FINAL REPORT"]);

    // Dapper refuses a result set without columns, so an empty one still names one.
    private static DataTable Empty() => Table(("fldUnused", typeof(int)));

    // The draft is 19 result sets: the header, then 18 that are empty when there is nothing else to publish.
    private static DataSet Draft(DataTable header) =>
        Sets([header, .. Enumerable.Range(0, 18).Select(_ => Empty())]);

    private static DataTable Recipients(params object[][] rows) =>
        Rows(Table(("fldEmail", typeof(string)), ("fldEmail2", typeof(string)), ("fldName", typeof(string))), rows);

    private static (ResultsSignOffRepository Repository, FakeDbConnection Connection) AwaitingWithSignatory()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(StateSql, 0);
        connection.RespondToQuery(SignatorySql, Signatories(["Jane", "Smith", "jsmith"]));
        connection.RespondToQuery(DraftSql, Draft(Header(TabulationA1, SchemeA)));
        connection.RespondToQuery(
            SchemeSql,
            Rows(Table(("fldMonthId", typeof(int)), ("fldYearId", typeof(int)), ("fldIsAccredited", typeof(bool)), ("fldRequiresAssessment", typeof(bool))), [6, 2026, true, false]));
        connection.RespondToQuery(IntendedResultsSql, Sets(Empty(), Empty()));
        connection.RespondToQuery(RecipientsSql, Sets(Recipients(), Recipients(), Recipients()));
        connection.RespondToNonQuery(PublishTabulationSql, 1);
        connection.RespondToNonQuery(QueueEmailSql, 1);
        connection.RespondToNonQuery(SetStateSql, 1);
        return (repository, connection);
    }

    [Fact]
    public async Task AuthoriseAsync_AwaitingSignOff_PublishesQueuesTheEmailsMarksItAuthorisedAndCommits()
    {
        var (repository, connection) = AwaitingWithSignatory();
        connection.RespondToQuery(RecipientsSql, Sets(
            Recipients(["p@example.org", "p2@example.org", "Lab One"], ["same@example.org", DBNull.Value, "Lab Two"]),
            Recipients(["viewer@example.org", DBNull.Value, "Viewer"], ["same@example.org", DBNull.Value, "Lab Two"]),
            Recipients(["viewer@example.org", DBNull.Value, "Viewer"], ["other@example.org", DBNull.Value, "Other"])));

        var result = await repository.AuthoriseAsync(TabulationA1, Signatory, Created);

        Assert.Equal(ResultsSignOffOutcome.Done, result.Outcome);
        var published = Assert.Single(connection.ExecutedCommands, c => c.CommandText == PublishTabulationSql);
        Assert.Equal(("Jane Smith", Created, TabulationA1, 6, 2026), (published.ParameterValue("Signatory"), published.ParameterValue("CreationDate"), published.ParameterValue("DistributionTabulationId"), published.ParameterValue("MonthId"), published.ParameterValue("YearId")));

        // Participants (their second address too), then the scheme's viewers, then the participants' viewers - once per address and name.
        var emails = connection.ExecutedCommands.Where(c => c.CommandText == QueueEmailSql).ToList();
        Assert.Equal(
            [("p@example.org", "Lab One"), ("p2@example.org", "Lab One"), ("same@example.org", "Lab Two"), ("viewer@example.org", "Viewer"), ("other@example.org", "Other")],
            emails.Select(e => ((string)e.ParameterValue("Email")!, (string)e.ParameterValue("Name")!)));
        Assert.All(emails, e => Assert.Equal(published.ParameterValue("PublishedTabulationId"), e.ParameterValue("PublishedTabulationId")));

        // The caller sends the emails of what was published, so it is told which publication that was.
        Assert.Equal(published.ParameterValue("PublishedTabulationId"), result.PublishedTabulationId);

        var update = Assert.Single(connection.ExecutedCommands, c => c.CommandText == SetStateSql);
        Assert.Equal((TabulationA1, 1), (update.ParameterValue("DistributionTabulationId"), update.ParameterValue("PublishState")));
        Assert.True(connection.LastTransaction!.Committed);
        Assert.False(connection.LastTransaction.RolledBack);
    }

    [Fact]
    public async Task AuthoriseAsync_QueuesTheEmailsAfterThePublishedRowsAndTheStateLast()
    {
        var (repository, connection) = AwaitingWithSignatory();
        connection.RespondToQuery(RecipientsSql, Sets(Recipients(["p@example.org", DBNull.Value, "Lab One"]), Recipients(), Recipients()));

        await repository.AuthoriseAsync(TabulationA1, Signatory, Created);

        Assert.Equal(
            [PublishTabulationSql, QueueEmailSql, SetStateSql],
            connection.ExecutedCommands.Select(c => c.CommandText).Where(t => t is not (DraftSql or SchemeSql or IntendedResultsSql or RecipientsSql or SignatorySql or StateSql)));
    }

    [Fact]
    public async Task AuthoriseAsync_SignatoryWithoutAName_IsSignedWithTheAccountName()
    {
        var (repository, connection) = AwaitingWithSignatory();
        connection.RespondToQuery(SignatorySql, Signatories([DBNull.Value, DBNull.Value, "jsmith"]));

        await repository.AuthoriseAsync(TabulationA1, Signatory, Created);

        Assert.Equal("jsmith", Assert.Single(connection.ExecutedCommands, c => c.CommandText == PublishTabulationSql).ParameterValue("Signatory"));
    }

    [Fact]
    public async Task AuthoriseAsync_UnknownTabulation_IsNotFoundAndRollsBack()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(StateSql, (object?)null);

        Assert.Equal(new AuthoriseResult(ResultsSignOffOutcome.NotFound), await repository.AuthoriseAsync(TabulationA1, Signatory, Created));

        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task AuthoriseAsync_AlreadyDecided_PublishesNothingAndRollsBack(int state)
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(StateSql, state);

        Assert.Equal(new AuthoriseResult(ResultsSignOffOutcome.NotAwaiting), await repository.AuthoriseAsync(TabulationA1, Signatory, Created));

        Assert.Equal(StateSql, Assert.Single(connection.ExecutedCommands).CommandText);
        Assert.True(connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task AuthoriseAsync_NoActingUser_IsRefusedWithoutReadingTheDraft()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(StateSql, 0);

        Assert.Equal(new AuthoriseResult(ResultsSignOffOutcome.NoSignatory), await repository.AuthoriseAsync(TabulationA1, null, Created));

        Assert.Equal(StateSql, Assert.Single(connection.ExecutedCommands).CommandText);
        Assert.True(connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task AuthoriseAsync_ActingUserNotInPtLims_IsRefusedAndRollsBack()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(StateSql, 0);
        connection.RespondToQuery(SignatorySql, Signatories());

        Assert.Equal(new AuthoriseResult(ResultsSignOffOutcome.NoSignatory), await repository.AuthoriseAsync(TabulationA1, Signatory, Created));

        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == DraftSql);
        Assert.True(connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task AuthoriseAsync_DraftWithoutAHeader_IsNotFoundAndRollsBack()
    {
        var (repository, connection) = AwaitingWithSignatory();
        connection.RespondToQuery(DraftSql, Draft(Table(("fldDistributionTabulationId", typeof(Guid)), ("fldMonthlyDistributionSchemeId", typeof(Guid)))));

        Assert.Equal(new AuthoriseResult(ResultsSignOffOutcome.NotFound), await repository.AuthoriseAsync(TabulationA1, Signatory, Created));

        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == PublishTabulationSql);
        Assert.True(connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task AuthoriseAsync_APublishedRowFails_RollsEverythingBackAndThrows()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToScalar(StateSql, 0);
        connection.RespondToQuery(SignatorySql, Signatories(["Jane", "Smith", "jsmith"]));
        connection.RespondToQuery(DraftSql, Draft(Header(TabulationA1, SchemeA)));
        connection.RespondToQuery(SchemeSql, Rows(Table(("fldMonthId", typeof(int)))));
        connection.RespondToQuery(IntendedResultsSql, Sets(Empty(), Empty()));

        // The published tabulation insert has no configured response, so the fake throws as that call fails.
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AuthoriseAsync(TabulationA1, Signatory, Created));

        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
        Assert.DoesNotContain(connection.ExecutedCommands, c => c.CommandText == SetStateSql);
    }

    private static readonly Guid FullTestId = Guid.Parse("66666666-0000-0000-0000-000000000001");
    private static readonly Guid FullSampleId = Guid.Parse("66666666-0000-0000-0000-000000000002");
    private static readonly Guid FullSampleTestId = Guid.Parse("66666666-0000-0000-0000-000000000003");
    private static readonly Guid FullMethodId = Guid.Parse("66666666-0000-0000-0000-000000000004");
    private static readonly Guid FullResultId = Guid.Parse("66666666-0000-0000-0000-000000000005");
    private static readonly Guid FullCategoryId = Guid.Parse("66666666-0000-0000-0000-000000000006");
    private static readonly Guid FullCriterionId = Guid.Parse("66666666-0000-0000-0000-000000000007");
    private static readonly Guid FullDistributionParticipantId = Guid.Parse("66666666-0000-0000-0000-000000000008");
    private static readonly Guid FullParticipantId = Guid.Parse("66666666-0000-0000-0000-000000000009");
    private static readonly Guid FullReceiptId = Guid.Parse("66666666-0000-0000-0000-00000000000a");
    private static readonly Guid FullAssessorId = Guid.Parse("66666666-0000-0000-0000-00000000000b");
    private static readonly Guid FullAttachmentId = Guid.Parse("66666666-0000-0000-0000-00000000000c");

    // Every draft result set filled in, so the whole read-and-map path from spgDistributionTabulation to the publish plan runs.
    private static DataSet FullDraft()
    {
        var assessed = new DateTime(2026, 9, 1);
        return Sets(
            Rows(
                Table(("fldDistributionTabulationId", typeof(Guid)), ("fldMonthlyDistributionSchemeId", typeof(Guid)), ("fldTabulationName", typeof(string)),
                    ("fldSchemeName", typeof(string)), ("fldSchemeIdentifier", typeof(string)), ("fldRequiresAssessment", typeof(bool)),
                    ("fldDistributionReference", typeof(string)), ("fldDistributionReferenceSuffix", typeof(string)), ("fldScheduleCode", typeof(string)),
                    ("fldComments", typeof(string)), ("fldTestConsultantComments", typeof(string)), ("fldTestConsultantCommentsAuthor", typeof(string)),
                    ("fldTestConsultantCommentsDept", typeof(string)), ("fldDistributionDate", typeof(DateTime)), ("fldSchemeVersionDate", typeof(DateTime)),
                    ("fldHasRatings", typeof(bool)), ("fldIntendedResultsOnly", typeof(bool)), ("fldSingleParticipantTabulation", typeof(bool)),
                    ("fldAvailableToParticipants", typeof(bool)), ("fldAvailableToViewers", typeof(bool)), ("fldStandardTabulationText", typeof(string))),
                [TabulationA1, SchemeA, "FINAL REPORT", "Brucella", "PT0001", true, "17001", "/BA", "PT0001", "Sample comments", "TC comments", "Dr TC", "APHA",
                    assessed, assessed, true, false, true, true, false, "Standard text"]),
            Rows(
                Table(("fldTestId", typeof(Guid)), ("fldTestType", typeof(string)), ("fldAssessmentSubmitted", typeof(DateTime)), ("fldAssessorId", typeof(Guid)), ("fldAssessorName", typeof(string))),
                [FullTestId, "Serology", assessed, FullAssessorId, "Ann Assessor"]),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestMethodItemId", typeof(Guid)), ("fldTestMethodItemType", typeof(string))), [FullTestId, FullMethodId, "ELISA"]),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)), ("fldTestResultItemType", typeof(string))), [FullTestId, FullResultId, "Titre"]),
            Rows(Table(("fldCategoryItemId", typeof(Guid)), ("fldTestId", typeof(Guid)), ("fldName", typeof(string))), [FullCategoryId, FullTestId, "Accuracy"]),
            Rows(
                Table(("fldTestId", typeof(Guid)), ("fldCategoryItemId", typeof(Guid)), ("fldCriterionItemId", typeof(Guid)), ("fldName", typeof(string))),
                [FullTestId, FullCategoryId, FullCriterionId, "Within range"]),
            Rows(
                Table(("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldSampleNumber", typeof(string)), ("fldTabulationDescription", typeof(string)), ("fldSampleTestId", typeof(Guid))),
                [FullTestId, FullSampleId, "S1", "Serum", FullSampleTestId]),
            Rows(
                Table(("fldDistributionParticipantId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldLabCode", typeof(string)), ("fldNilReturnReason", typeof(string))),
                [FullDistributionParticipantId, FullParticipantId, "L001", "None"]),
            Rows(Table(("fldSampleId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldRating", typeof(string))), [FullSampleId, FullParticipantId, "Good"]),
            Rows(
                Table(("fldParticipantId", typeof(Guid)), ("fldTestMethodItemId", typeof(Guid)), ("fldAmendedResult", typeof(string))),
                [FullParticipantId, FullMethodId, "Positive"]),
            Rows(
                Table(("fldSampleId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)), ("fldAmendedResult", typeof(string))),
                [FullSampleId, FullParticipantId, FullResultId, "1:64"]),
            Rows(
                Table(("fldParticipantId", typeof(Guid)), ("fldCategoryItemId", typeof(Guid)), ("fldSampleTestId", typeof(Guid)), ("fldCategoryScore", typeof(int)),
                    ("fldSampleId", typeof(Guid)), ("fldTestId", typeof(Guid)), ("fldAssessmentSubmitted", typeof(DateTime))),
                [FullParticipantId, FullCategoryId, FullSampleTestId, 3, FullSampleId, FullTestId, assessed]),
            Rows(
                Table(("fldParticipantId", typeof(Guid)), ("fldCategoryItemId", typeof(Guid)), ("fldTestId", typeof(Guid)), ("fldCriterionItemId", typeof(Guid)),
                    ("fldSampleId", typeof(Guid)), ("fldName", typeof(string))),
                [FullParticipantId, FullCategoryId, FullTestId, FullCriterionId, FullSampleId, "Within range"]),
            Rows(Table(("fldTestMethodItemId", typeof(Guid)), ("fldIntendedResult", typeof(string))), [FullMethodId, "Positive"]),
            Rows(
                Table(("fldSampleId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)), ("fldIntendedResult", typeof(string)), ("fldIntendedResult2", typeof(string))),
                [FullSampleId, FullResultId, "1:32", "1:64"]),
            Empty(),
            Empty(),
            Rows(
                Table(("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldSampleReceiptId", typeof(Guid)), ("fldSampleTestId", typeof(Guid)), ("fldParticipantId", typeof(Guid)),
                    ("fldScore", typeof(int)), ("fldAssessmentComment", typeof(string)), ("fldAssessorId", typeof(Guid)), ("fldAssessorName", typeof(string)),
                    ("fldAssessmentDate", typeof(DateTime)), ("fldSubmissionDate", typeof(DateTime)), ("fldNonSubmissionDate", typeof(DateTime)),
                    ("fldNonCapableDate", typeof(DateTime)), ("fldAssessmentSubmitted", typeof(DateTime))),
                [FullTestId, FullSampleId, FullReceiptId, FullSampleTestId, FullParticipantId, 4, "Fine", FullAssessorId, "Ann Assessor", assessed, assessed, assessed, assessed, assessed]),
            Rows(
                Table(("fldMonthlyDistributionAttachmentId", typeof(Guid)), ("fldFileName", typeof(string)), ("fldContentType", typeof(string)),
                    ("fldFileData", typeof(byte[])), ("fldUploadDate", typeof(DateTime))),
                [FullAttachmentId, "report.pdf", "application/pdf", new byte[] { 1, 2, 3 }, assessed]));
    }

    [Fact]
    public async Task AuthoriseAsync_FullDraft_PublishesEveryRowTheDraftHolds()
    {
        var (repository, connection) = AwaitingWithSignatory();
        connection.DefaultNonQueryResult = 1;
        connection.RespondToQuery(DraftSql, FullDraft());
        connection.RespondToQuery(
            SchemeSql,
            Rows(Table(("fldMonthId", typeof(int)), ("fldYearId", typeof(int)), ("fldIsAccredited", typeof(bool)), ("fldRequiresAssessment", typeof(bool))), [6, 2026, true, true]));
        connection.RespondToQuery(
            IntendedResultsSql,
            Sets(Empty(), Rows(Table(("fldSampleId", typeof(Guid)), ("fldSampleNumber", typeof(string)), ("fldTabulationDescription", typeof(string))), [FullSampleId, "S1", "Serum"])));

        var result = await repository.AuthoriseAsync(TabulationA1, Signatory, Created);

        Assert.Equal(ResultsSignOffOutcome.Done, result.Outcome);
        FakeDbCommand Published(string procedure) =>
            Assert.Single(connection.ExecutedCommands, c => c.CommandText.StartsWith($"EXEC dbo.{procedure} ", StringComparison.Ordinal));

        var header = Published("spiPublishedTabulation");
        Assert.Equal(
            ("FINAL REPORT", "17001/BA/PT0001", "PT0001", "Brucella", "Sample comments", "TC comments", "Dr TC", "APHA", true, true, "Standard text"),
            ((string)header.ParameterValue("Name")!, (string)header.ParameterValue("Distribution")!, (string)header.ParameterValue("SchemeIdentifier")!, (string)header.ParameterValue("SchemeName")!,
                (string)header.ParameterValue("SampleComments")!, (string)header.ParameterValue("TestConsultantComments")!, (string)header.ParameterValue("TestConsultantCommentsAuthor")!,
                (string)header.ParameterValue("TestConsultantCommentsDept")!, header.ParameterValue("HasRatings")!, header.ParameterValue("RequiresAssessment")!, (string)header.ParameterValue("StandardTabulationText")!));

        var sample = Published("spiPublishedSample");
        Assert.Equal(("S1", "Serum"), ((string)sample.ParameterValue("SampleNumber")!, (string)sample.ParameterValue("TabulationDescription")!));

        var test = Published("spiPublishedTest");
        Assert.Equal(("Serology", "Ann Assessor", FullAssessorId), ((string)test.ParameterValue("Name")!, (string)test.ParameterValue("AssessorName")!, test.ParameterValue("AssessorId")!));
        Assert.Equal(sample.ParameterValue("PublishedSampleId"), Published("spiPublishedSampleTest").ParameterValue("PublishedSampleId"));
        Assert.Equal("Titre", Published("spiPublishedTestResultItem").ParameterValue("Name"));
        Assert.Equal("ELISA", Published("spiPublishedTestMethodItem").ParameterValue("Name"));
        Assert.Equal("Accuracy", Published("spiPublishedCategoryItem").ParameterValue("Name"));
        Assert.Equal("Within range", Published("spiPublishedCriterionItem").ParameterValue("Name"));

        Assert.Equal("Positive", Published("spiPublishedIntendedMethodItem").ParameterValue("Result"));
        var intended = Published("spiPublishedIntendedResultItem");
        Assert.Equal(("1:32", "1:64"), ((string)intended.ParameterValue("Result")!, (string)intended.ParameterValue("Result2")!));

        var participant = Published("spiPublishedParticipant");
        Assert.Equal(("L001", "None"), ((string)participant.ParameterValue("LabCode")!, (string)participant.ParameterValue("NilReturnReason")!));
        Assert.Equal("Good", Published("spiPublishedSampleRating").ParameterValue("Rating"));
        Assert.Equal("Positive", Published("spiPublishedParticipantMethodItem").ParameterValue("Result"));
        Assert.Equal("1:64", Published("spiPublishedParticipantResultItem").ParameterValue("Result"));

        var assessment = Published("spiPublishedParticipantSampleTest");
        Assert.Equal((FullReceiptId, "Fine", 4, "Ann Assessor"), (assessment.ParameterValue("SampleReceiptId")!, (string)assessment.ParameterValue("AssessmentComment")!, assessment.ParameterValue("AssessmentScore")!, (string)assessment.ParameterValue("AssessorName")!));
        Assert.Equal(3, Published("spiPublishedParticipantCategoryItem").ParameterValue("Score"));
        Assert.NotNull(Published("spiPublishedParticipantCriterionItem").ParameterValue("PublishedCriterionItemId"));

        var attachment = Published("spiPublishedAttachment");
        Assert.Equal(("report.pdf", "application/pdf", FullAttachmentId), ((string)attachment.ParameterValue("FileName")!, (string)attachment.ParameterValue("ContentType")!, attachment.ParameterValue("AttachmentId")!));
        Assert.Equal(new byte[] { 1, 2, 3 }, (byte[])attachment.ParameterValue("FileData")!);

        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task AuthoriseAsync_DraftWithNullColumns_PublishesEmptyValuesInsteadOfNulls()
    {
        var (repository, connection) = AwaitingWithSignatory();
        connection.DefaultNonQueryResult = 1;
        var noValue = DBNull.Value;
        connection.RespondToQuery(DraftSql, Sets(
            Rows(
                Table(("fldDistributionTabulationId", typeof(Guid)), ("fldMonthlyDistributionSchemeId", typeof(Guid)), ("fldTabulationName", typeof(string)), ("fldRequiresAssessment", typeof(bool))),
                [TabulationA1, SchemeA, noValue, noValue]),
            Rows(
                Table(("fldTestId", typeof(Guid)), ("fldTestType", typeof(string)), ("fldAssessorId", typeof(Guid)), ("fldAssessorName", typeof(string))),
                [FullTestId, noValue, noValue, noValue]),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestMethodItemId", typeof(Guid)), ("fldTestMethodItemType", typeof(string))), [FullTestId, FullMethodId, noValue]),
            Rows(Table(("fldTestId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)), ("fldTestResultItemType", typeof(string))), [FullTestId, FullResultId, noValue]),
            Rows(Table(("fldCategoryItemId", typeof(Guid)), ("fldTestId", typeof(Guid)), ("fldName", typeof(string))), [FullCategoryId, FullTestId, noValue]),
            Rows(
                Table(("fldTestId", typeof(Guid)), ("fldCategoryItemId", typeof(Guid)), ("fldCriterionItemId", typeof(Guid)), ("fldName", typeof(string))),
                [FullTestId, FullCategoryId, FullCriterionId, noValue]),
            Rows(
                Table(("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldSampleNumber", typeof(string)), ("fldTabulationDescription", typeof(string)), ("fldSampleTestId", typeof(Guid))),
                [FullTestId, FullSampleId, noValue, noValue, FullSampleTestId]),
            Rows(
                Table(("fldDistributionParticipantId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldLabCode", typeof(string)), ("fldNilReturnReason", typeof(string))),
                [FullDistributionParticipantId, FullParticipantId, noValue, noValue]),
            Rows(Table(("fldSampleId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldRating", typeof(string))), [FullSampleId, FullParticipantId, noValue]),
            Rows(Table(("fldParticipantId", typeof(Guid)), ("fldTestMethodItemId", typeof(Guid)), ("fldAmendedResult", typeof(string))), [FullParticipantId, FullMethodId, noValue]),
            Rows(
                Table(("fldSampleId", typeof(Guid)), ("fldParticipantId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)), ("fldAmendedResult", typeof(string))),
                [FullSampleId, FullParticipantId, FullResultId, noValue]),
            Rows(
                Table(("fldParticipantId", typeof(Guid)), ("fldCategoryItemId", typeof(Guid)), ("fldSampleTestId", typeof(Guid)), ("fldCategoryScore", typeof(int)),
                    ("fldSampleId", typeof(Guid)), ("fldTestId", typeof(Guid))),
                [FullParticipantId, FullCategoryId, FullSampleTestId, noValue, FullSampleId, FullTestId]),
            Rows(
                Table(("fldParticipantId", typeof(Guid)), ("fldCategoryItemId", typeof(Guid)), ("fldTestId", typeof(Guid)), ("fldCriterionItemId", typeof(Guid)),
                    ("fldSampleId", typeof(Guid)), ("fldName", typeof(string))),
                [FullParticipantId, FullCategoryId, FullTestId, FullCriterionId, FullSampleId, noValue]),
            Rows(Table(("fldTestMethodItemId", typeof(Guid)), ("fldIntendedResult", typeof(string))), [FullMethodId, noValue]),
            Rows(
                Table(("fldSampleId", typeof(Guid)), ("fldTestResultItemId", typeof(Guid)), ("fldIntendedResult", typeof(string)), ("fldIntendedResult2", typeof(string))),
                [FullSampleId, FullResultId, noValue, noValue]),
            Empty(),
            Empty(),
            Rows(
                Table(("fldTestId", typeof(Guid)), ("fldSampleId", typeof(Guid)), ("fldSampleReceiptId", typeof(Guid)), ("fldSampleTestId", typeof(Guid)), ("fldParticipantId", typeof(Guid)),
                    ("fldScore", typeof(int)), ("fldAssessmentComment", typeof(string)), ("fldAssessorId", typeof(Guid)), ("fldAssessorName", typeof(string))),
                [FullTestId, FullSampleId, FullReceiptId, FullSampleTestId, FullParticipantId, noValue, noValue, noValue, noValue]),
            Rows(
                Table(("fldMonthlyDistributionAttachmentId", typeof(Guid)), ("fldFileName", typeof(string)), ("fldContentType", typeof(string)), ("fldFileData", typeof(byte[]))),
                [FullAttachmentId, noValue, noValue, noValue])));
        connection.RespondToQuery(SchemeSql, Rows(Table(("fldMonthId", typeof(int)), ("fldYearId", typeof(int)), ("fldIsAccredited", typeof(bool)), ("fldRequiresAssessment", typeof(bool))), [noValue, noValue, noValue, noValue]));
        connection.RespondToQuery(
            IntendedResultsSql,
            Sets(Empty(), Rows(Table(("fldSampleId", typeof(Guid)), ("fldSampleNumber", typeof(string)), ("fldTabulationDescription", typeof(string))), [FullSampleId, noValue, noValue])));

        var result = await repository.AuthoriseAsync(TabulationA1, Signatory, Created);

        Assert.Equal(ResultsSignOffOutcome.Done, result.Outcome);
        var header = Assert.Single(connection.ExecutedCommands, c => c.CommandText.StartsWith("EXEC dbo.spiPublishedTabulation ", StringComparison.Ordinal));
        Assert.Equal((string.Empty, 0, 0, false), ((string)header.ParameterValue("Name")!, header.ParameterValue("MonthId")!, header.ParameterValue("YearId")!, header.ParameterValue("IsAccredited")!));
        var sample = Assert.Single(connection.ExecutedCommands, c => c.CommandText.StartsWith("EXEC dbo.spiPublishedSample ", StringComparison.Ordinal));
        Assert.Equal((string.Empty, string.Empty), ((string)sample.ParameterValue("SampleNumber")!, (string)sample.ParameterValue("TabulationDescription")!));
        Assert.Equal(string.Empty, Assert.Single(connection.ExecutedCommands, c => c.CommandText.StartsWith("EXEC dbo.spiPublishedTest ", StringComparison.Ordinal)).ParameterValue("Name"));
        Assert.Equal(string.Empty, Assert.Single(connection.ExecutedCommands, c => c.CommandText.StartsWith("EXEC dbo.spiPublishedParticipant ", StringComparison.Ordinal)).ParameterValue("LabCode"));
        Assert.True(connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task AuthoriseAsync_SchemeRowMissing_PublishesWithZeroMonthAndYear()
    {
        var (repository, connection) = AwaitingWithSignatory();
        connection.RespondToQuery(SchemeSql, Rows(Table(("fldMonthId", typeof(int)), ("fldYearId", typeof(int)), ("fldIsAccredited", typeof(bool)), ("fldRequiresAssessment", typeof(bool)))));

        var result = await repository.AuthoriseAsync(TabulationA1, Signatory, Created);

        Assert.Equal(ResultsSignOffOutcome.Done, result.Outcome);
        var published = Assert.Single(connection.ExecutedCommands, c => c.CommandText == PublishTabulationSql);
        Assert.Equal((0, 0), (published.ParameterValue("MonthId"), published.ParameterValue("YearId")));
    }

    [Fact]
    public async Task AuthoriseAsync_RecipientsWithNoAddress_AreQueuedWithEmptyValues()
    {
        var (repository, connection) = AwaitingWithSignatory();
        connection.RespondToQuery(RecipientsSql, Sets(
            Recipients([DBNull.Value, string.Empty, DBNull.Value]),
            Recipients([DBNull.Value, DBNull.Value, DBNull.Value]),
            Recipients()));

        await repository.AuthoriseAsync(TabulationA1, Signatory, Created);

        var email = Assert.Single(connection.ExecutedCommands, c => c.CommandText == QueueEmailSql);
        Assert.Equal((string.Empty, string.Empty), (email.ParameterValue("Email"), email.ParameterValue("Name")));
    }
}
