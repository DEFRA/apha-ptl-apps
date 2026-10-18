using System.Data;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// The Test Consultant and publication emails - legacy spgaMonthlyDistributionSchemeForEmail,
// spgaUserAllTestConsultant, spuMonthlyDistributionSchemeForEmail, spgaNotificationEmail, spuNotificationEmail.
public class DistributionRepositoryTabulationEmailTests
{
    public DistributionRepositoryTabulationEmailTests() => DapperColumnMappings.Register();

    private const string CandidatesSql = """
        SELECT
            m.fldMonthlyDistributionSchemeId,
            s.fldSchemeId,
            s.fldName AS fldSchemeName,
            m.fldDistributionReference,
            m.fldDistributionReferenceSuffix,
            m.fldScheduleCode,
            m.fldEmailState,
            m.fldIsCommentSubmitted,
            m.fldIsReadyForTestConsultant,
            s.fldTestConsultant1 AS fldTestConsultant1Id,
            s.fldTestConsultant2 AS fldTestConsultant2Id,
            s.fldTestConsultant3 AS fldTestConsultant3Id
        FROM
            tblMonthlyDistributionScheme m
        INNER JOIN tblScheme s ON m.fldSchemeId = s.fldSchemeId
        WHERE
            m.fldIsReadyForTestConsultant >= @From AND
            m.fldIsReadyForTestConsultant < @ToExclusive
        ORDER BY
            m.fldDistributionReference, m.fldDistributionReferenceSuffix
        """;

    private const string ConsultantsSql = "EXEC dbo.spgaUserAllTestConsultant";
    private const string SetStateSql = "EXEC dbo.spuMonthlyDistributionSchemeForEmail @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId, @EmailState = @EmailState";
    private const string UnsentSql = "EXEC dbo.spgaNotificationEmail";
    private const string MarkSentSql = "EXEC dbo.spuNotificationEmail @NotificationEmailId = @NotificationEmailId, @DateSent = @DateSent";

    private static readonly Guid MdsId1 = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid MdsId2 = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid SchemeId1 = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid SchemeId2 = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid Consultant1 = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid Consultant2 = Guid.Parse("33333333-0000-0000-0000-000000000002");
    private static readonly Guid Consultant3 = Guid.Parse("33333333-0000-0000-0000-000000000003");
    private static readonly Guid NotificationId = Guid.Parse("44444444-0000-0000-0000-000000000001");
    private static readonly Guid PublishedId = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly DateTime Ready = new(2026, 5, 4, 9, 30, 0);

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    // The SQL is matched exactly, so register both line-ending forms of the multi-line text.
    private static void RespondToCandidates(FakeDbConnection connection, DataTable table)
    {
        connection.RespondToQuery(CandidatesSql.ReplaceLineEndings("\n"), table);
        connection.RespondToQuery(CandidatesSql.ReplaceLineEndings("\r\n"), table);
    }

    private static DataTable CandidateTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldSchemeId", typeof(Guid));
        table.Columns.Add("fldSchemeName", typeof(string));
        table.Columns.Add("fldDistributionReference", typeof(string));
        table.Columns.Add("fldDistributionReferenceSuffix", typeof(string));
        table.Columns.Add("fldScheduleCode", typeof(string));
        table.Columns.Add("fldEmailState", typeof(int));
        table.Columns.Add("fldIsCommentSubmitted", typeof(bool));
        table.Columns.Add("fldIsReadyForTestConsultant", typeof(DateTime));
        table.Columns.Add("fldTestConsultant1Id", typeof(Guid));
        table.Columns.Add("fldTestConsultant2Id", typeof(Guid));
        table.Columns.Add("fldTestConsultant3Id", typeof(Guid));
        return table;
    }

    private static DataTable ConsultantTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldUserId", typeof(Guid));
        table.Columns.Add("fldFriendlyName", typeof(string));
        table.Columns.Add("fldDepartment", typeof(string));
        table.Columns.Add("fldEmail", typeof(string));
        return table;
    }

    private static DataTable NotificationTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldNotificationEmailId", typeof(Guid));
        table.Columns.Add("fldPublishedTabulationId", typeof(Guid));
        table.Columns.Add("fldEmail", typeof(string));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldDistribution", typeof(string));
        table.Columns.Add("fldScheme", typeof(string));
        return table;
    }

    // ---- GetTabulationEmailCandidatesAsync ----

    [Fact]
    public async Task GetTabulationEmailCandidatesAsync_MapsEveryColumnIncludingNullConsultants()
    {
        var (repository, connection) = CreateRepository();
        var table = CandidateTable();
        table.Rows.Add(MdsId1, SchemeId1, "Brucella", "D1", "a", "S1", 1, true, Ready, Consultant1, Consultant2, Consultant3);
        table.Rows.Add(MdsId2, SchemeId2, "Mastitis", "D2", "", "S2", 0, false, Ready.AddDays(1), Consultant1, DBNull.Value, DBNull.Value);
        RespondToCandidates(connection, table);

        var candidates = await repository.GetTabulationEmailCandidatesAsync(Ready.Date, Ready.Date.AddDays(10));

        Assert.Equal(2, candidates.Count);
        var first = candidates[0];
        Assert.Equal((MdsId1, SchemeId1, "Brucella", "D1", "a", "S1"),
            (first.MonthlyDistributionSchemeId, first.SchemeId, first.SchemeName, first.DistributionReference, first.DistributionReferenceSuffix, first.ScheduleCode));
        Assert.Equal((1, true, Ready), (first.EmailState, first.IsCommentSubmitted, first.IsReadyForTestConsultant));
        Assert.Equal((Consultant1, Consultant2, Consultant3), (first.TestConsultant1Id, first.TestConsultant2Id, first.TestConsultant3Id));
        Assert.Equal("D1a/S1", first.ReferenceFull);

        var second = candidates[1];
        Assert.Equal(MdsId2, second.MonthlyDistributionSchemeId);
        Assert.Equal((0, false), (second.EmailState, second.IsCommentSubmitted));
        Assert.Equal(Consultant1, second.TestConsultant1Id);
        Assert.Null(second.TestConsultant2Id);
        Assert.Null(second.TestConsultant3Id);
    }

    [Fact]
    public async Task GetTabulationEmailCandidatesAsync_PassesTheReadyDateRangeAsParameters()
    {
        var (repository, connection) = CreateRepository();
        RespondToCandidates(connection, CandidateTable());
        var from = new DateTime(2026, 5, 1);
        var toExclusive = new DateTime(2026, 5, 11);

        var candidates = await repository.GetTabulationEmailCandidatesAsync(from, toExclusive);

        Assert.Empty(candidates);
        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Contains("m.fldIsReadyForTestConsultant >= @From", command.CommandText);
        Assert.Contains("m.fldIsReadyForTestConsultant < @ToExclusive", command.CommandText);
        Assert.Equal((from, toExclusive), (command.ParameterValue("From"), command.ParameterValue("ToExclusive")));
    }

    // ---- GetTabulationEmailConsultantsAsync ----

    [Fact]
    public async Task GetTabulationEmailConsultantsAsync_FlagsOnlyTheSecondResultSetAsExternal()
    {
        var (repository, connection) = CreateRepository();
        var internalUsers = ConsultantTable();
        internalUsers.Rows.Add(Consultant1, "Internal One", "Epidemiology", "internal1@example.test");
        internalUsers.Rows.Add(Consultant2, "Internal Two", DBNull.Value, DBNull.Value);
        var externalUsers = ConsultantTable();
        externalUsers.Rows.Add(Consultant3, "External Three", "University", "external3@example.test");
        var dataSet = new DataSet();
        dataSet.Tables.AddRange([internalUsers, externalUsers]);
        connection.RespondToQuery(ConsultantsSql, dataSet);

        var consultants = await repository.GetTabulationEmailConsultantsAsync();

        Assert.Equal([Consultant1, Consultant2, Consultant3], consultants.Select(c => c.UserId));
        Assert.Equal([false, false, true], consultants.Select(c => c.IsExternal));
        Assert.Equal(("Internal One", "Epidemiology", "internal1@example.test"), (consultants[0].FriendlyName, consultants[0].Department, consultants[0].Email));
        Assert.Equal("Internal Two", consultants[1].FriendlyName);
        Assert.Null(consultants[1].Department);
        Assert.Null(consultants[1].Email);
        Assert.Equal(("External Three", "University", "external3@example.test"), (consultants[2].FriendlyName, consultants[2].Department, consultants[2].Email));
        Assert.Equal(ConsultantsSql, Assert.Single(connection.ExecutedCommands).CommandText);
    }

    [Fact]
    public async Task GetTabulationEmailConsultantsAsync_NoRows_ReturnsEmpty()
    {
        var (repository, connection) = CreateRepository();
        var dataSet = new DataSet();
        dataSet.Tables.AddRange([ConsultantTable(), ConsultantTable()]);
        connection.RespondToQuery(ConsultantsSql, dataSet);

        Assert.Empty(await repository.GetTabulationEmailConsultantsAsync());
    }

    // ---- SetTabulationEmailStateAsync ----

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SetTabulationEmailStateAsync_RunsTheUpdateProcedureWithTheSchemeAndState(int state)
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(SetStateSql, 1);

        await repository.SetTabulationEmailStateAsync(MdsId1, state);

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(SetStateSql, command.CommandText);
        Assert.Equal((MdsId1, state), (command.ParameterValue("MonthlyDistributionSchemeId"), command.ParameterValue("EmailState")));
    }

    // ---- GetUnsentNotificationEmailsAsync ----

    [Fact]
    public async Task GetUnsentNotificationEmailsAsync_MapsTheNotificationColumns()
    {
        var (repository, connection) = CreateRepository();
        var table = NotificationTable();
        table.Rows.Add(NotificationId, PublishedId, "lab@example.test", "Lab One", "17152A/BA", "PT0060: Mastitis bovine");
        connection.RespondToQuery(UnsentSql, table);

        var emails = await repository.GetUnsentNotificationEmailsAsync();

        var email = Assert.Single(emails);
        Assert.Equal((NotificationId, PublishedId), (email.NotificationEmailId, email.PublishedTabulationId));
        Assert.Equal(("lab@example.test", "Lab One", "17152A/BA", "PT0060: Mastitis bovine"), (email.Email, email.Name, email.Distribution, email.Scheme));
        Assert.Equal(UnsentSql, Assert.Single(connection.ExecutedCommands).CommandText);
    }

    [Fact]
    public async Task GetUnsentNotificationEmailsAsync_NoRows_ReturnsEmpty()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(UnsentSql, NotificationTable());

        Assert.Empty(await repository.GetUnsentNotificationEmailsAsync());
    }

    // ---- MarkNotificationEmailSentAsync ----

    [Fact]
    public async Task MarkNotificationEmailSentAsync_RunsTheUpdateProcedureWithTheIdAndSentDate()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(MarkSentSql, 1);
        var sent = new DateTime(2026, 5, 14, 10, 0, 0);

        await repository.MarkNotificationEmailSentAsync(NotificationId, sent);

        var command = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(MarkSentSql, command.CommandText);
        Assert.Equal((NotificationId, sent), (command.ParameterValue("NotificationEmailId"), command.ParameterValue("DateSent")));
    }
}
