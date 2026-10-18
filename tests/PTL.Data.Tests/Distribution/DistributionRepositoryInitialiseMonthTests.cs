using System.Data;
using PTL.Core.Distribution;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// Legacy SetupMonthlyDistribution: one transaction around the existence check, the setup procedure
// and the spiMonthlyDistribution / spiMonthlyDistributionScheme / spiDistributionParticipant inserts.
public class DistributionRepositoryInitialiseMonthTests
{
    private const string ExistsSql = "EXEC dbo.spgMonthlyDistribution @YearId = @YearId, @MonthId = @MonthId";
    private const string SetupSql = "EXEC dbo.sppSetupMonthlyDistribution @YearId = @YearId, @MonthId = @MonthId";
    private const string InsertMonthSql = "EXEC dbo.spiMonthlyDistribution @MonthlyDistributionId = @MonthlyDistributionId, @YearId = @YearId, @MonthId = @MonthId";

    private const string InsertSchemeSql = """
        EXEC dbo.spiMonthlyDistributionScheme
            @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
            @MonthlyDistributionId = @MonthlyDistributionId,
            @SchemeId = @SchemeId,
            @MonthlyDistributionReference = @DistributionReference,
            @MonthlyDistributionReferenceSuffix = @DistributionReferenceSuffix,
            @MonthlyDistributionDate = @DistributionDate,
            @DeadlineDate = @DeadlineDate,
            @OverseasPostingDate = @OverseasPostingDate,
            @ResultsIssueTargetDate = @ResultsIssueTargetDate,
            @Comments = @Comments,
            @HasIntendedResults = @HasIntendedResults,
            @HasSampleNumbersDefined = @HasSampleNumbersDefined,
            @IsCancelled = @IsCancelled,
            @StoreRatings = @StoreRatings,
            @SchemeVersionDate = @SchemeVersionDate
        """;

    private const string InsertParticipantSql = """
        EXEC dbo.spiDistributionParticipant
            @DistributionParticipantId = @DistributionParticipantId,
            @ParticipantId = @ParticipantId,
            @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId,
            @DeadlineDate = @DeadlineDate,
            @DeadlineChangeReason = @DeadlineChangeReason,
            @AdditionalParticipant = @AdditionalParticipant
        """;

    private static readonly Guid SchemeA = Guid.Parse("11111111-0000-0000-0000-00000000000a");
    private static readonly Guid SchemeB = Guid.Parse("11111111-0000-0000-0000-00000000000b");

    public DistributionRepositoryInitialiseMonthTests() => DapperColumnMappings.Register();

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static DataSet ExistsResponse(Guid monthlyDistributionId)
    {
        var header = new DataTable();
        header.Columns.Add("fldMonthlyDistributionId", typeof(Guid));
        header.Columns.Add("fldYearId", typeof(int));
        header.Columns.Add("fldMonthId", typeof(int));
        header.Rows.Add(monthlyDistributionId, 2026, 5);

        var dataSet = new DataSet();
        dataSet.Tables.Add(header);
        return dataSet;
    }

    private static DataSet SetupResponse()
    {
        var schemes = new DataTable();
        schemes.Columns.Add("fldSchemeId", typeof(Guid));
        schemes.Columns.Add("fldWeekNumber", typeof(int));
        schemes.Columns.Add("fldDeadline", typeof(int));
        schemes.Columns.Add("fldCommentsRequired", typeof(bool));
        schemes.Columns.Add("DistributionReference", typeof(int));
        schemes.Columns.Add("fldScheduleCode", typeof(string));
        schemes.Columns.Add("fldDayOfWeek", typeof(int));
        schemes.Columns.Add("fldName", typeof(string));
        schemes.Columns.Add("fldIdentifier", typeof(string));
        schemes.Columns.Add("fldNumberOfSamples", typeof(int));
        schemes.Columns.Add("fldIsCancelled", typeof(bool));
        schemes.Columns.Add("fldIsAsAvailable", typeof(bool));
        schemes.Columns.Add("fldStoreRatings", typeof(bool));
        schemes.Columns.Add("fldCombinedPackaging", typeof(bool));
        schemes.Columns.Add("fldVersionDate", typeof(DateTime));
        schemes.Rows.Add(SchemeA, 1, 14, false, 17152, "BA", 0, "Mastitis bovine", "PT0060", 4, false, false, true, false, new DateTime(2025, 11, 3));
        schemes.Rows.Add(SchemeB, 2, 21, true, 17153, "BB", 3, "Brucella", "PT0070", 2, true, true, false, true, new DateTime(2025, 12, 1));

        var participants = new DataTable();
        participants.Columns.Add("fldParticipantId", typeof(Guid));
        participants.Columns.Add("fldCustomerId", typeof(Guid));
        participants.Columns.Add("fldLabCode", typeof(string));
        participants.Columns.Add("fldLabName", typeof(string));
        participants.Columns.Add("fldExternalReference", typeof(string));
        participants.Columns.Add("fldContactName", typeof(string));
        participants.Columns.Add("SchemeId", typeof(Guid));
        participants.Columns.Add("fldNumberOfSetsRequired", typeof(int));
        participants.Columns.Add("fldContractId", typeof(Guid));
        participants.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), "L001", "Lab One", "REF1", "Ann", SchemeA, 2, Guid.NewGuid());
        participants.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), "L002", "Lab Two", "REF2", "Bob", SchemeB, 1, Guid.NewGuid());
        participants.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), "L003", "Lab Three", "REF3", "Cat", SchemeA, 3, Guid.NewGuid());

        var dataSet = new DataSet();
        dataSet.Tables.Add(schemes);
        dataSet.Tables.Add(participants);
        return dataSet;
    }

    private static MonthSetupPlanner Planner(List<MonthSetupSchemeRow> schemes, List<MonthSetupParticipantRow> participants) =>
        (s, p) =>
        {
            schemes.AddRange(s);
            participants.AddRange(p);
            return MonthlyDistributionPlanner.Build(2026, 5, s, p);
        };

    [Fact]
    public async Task InitialiseMonthAsync_NullPlanner_Throws()
    {
        var (repository, _) = CreateRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.InitialiseMonthAsync(2026, 5, null!));
    }

    [Fact]
    public async Task InitialiseMonthAsync_MonthAlreadyExists_WritesNothingAndRollsBack()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(ExistsSql, ExistsResponse(Guid.NewGuid()));
        var planned = false;

        var result = await repository.InitialiseMonthAsync(2026, 5, (_, _) =>
        {
            planned = true;
            throw new InvalidOperationException("must not plan");
        });

        Assert.False(result);
        Assert.False(planned);
        Assert.True(connection.LastTransaction!.RolledBack);
        Assert.False(connection.LastTransaction.Committed);
        Assert.Equal([ExistsSql], connection.ExecutedCommands.Select(c => c.CommandText));
        Assert.Equal(2026, connection.ExecutedCommands[0].ParameterValue("YearId"));
        Assert.Equal(5, connection.ExecutedCommands[0].ParameterValue("MonthId"));
    }

    [Fact]
    public async Task InitialiseMonthAsync_NewMonth_SeedsThenInsertsTheMonthEverySchemeAndItsParticipantsInOneTransaction()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(ExistsSql, ExistsResponse(Guid.Empty));
        connection.RespondToQuery(SetupSql, SetupResponse());
        connection.RespondToNonQuery(InsertMonthSql, 1);
        connection.RespondToNonQuery(InsertSchemeSql, 1);
        connection.RespondToNonQuery(InsertParticipantSql, 1);
        var schemeRows = new List<MonthSetupSchemeRow>();
        var participantRows = new List<MonthSetupParticipantRow>();

        var result = await repository.InitialiseMonthAsync(2026, 5, Planner(schemeRows, participantRows));

        Assert.True(result);
        Assert.True(connection.LastTransaction!.Committed);

        var first = schemeRows[0];
        Assert.Equal((SchemeA, 1, 14, false, 17152, "BA", 0), (first.SchemeId, first.WeekNumber, first.DeadlineDays, first.CommentsRequired, first.DistributionReference, first.ScheduleCode, first.DayOffset));
        Assert.Equal(("Mastitis bovine", "PT0060", 4, false, true, false, new DateTime(2025, 11, 3)), (first.SchemeName, first.SchemeIdentifier, first.NumberOfSamples, first.IsAsAvailable, first.StoreRatings, first.CombinedPackaging, first.VersionDate));
        Assert.True(schemeRows[1].CombinedPackaging);
        Assert.Equal(3, participantRows.Count);
        Assert.Equal((SchemeA, "L001", "Lab One", 2), (participantRows[0].SchemeId, participantRows[0].LabCode, participantRows[0].LabName, participantRows[0].NumberOfSetsRequired));

        // Legacy order: the month, then each scheme followed by its own participants.
        Assert.Equal(
            [ExistsSql, SetupSql, InsertMonthSql, InsertSchemeSql, InsertParticipantSql, InsertParticipantSql, InsertSchemeSql, InsertParticipantSql],
            connection.ExecutedCommands.Select(c => c.CommandText));

        var month = connection.ExecutedCommands[2];
        Assert.Equal(2026, month.ParameterValue("YearId"));
        Assert.Equal(5, month.ParameterValue("MonthId"));
        var monthId = (Guid)month.ParameterValue("MonthlyDistributionId")!;
        Assert.NotEqual(Guid.Empty, monthId);

        var schemeCommands = connection.ExecutedCommands.Where(c => c.CommandText == InsertSchemeSql).ToList();
        Assert.All(schemeCommands, c => Assert.Equal(monthId, c.ParameterValue("MonthlyDistributionId")));
        Assert.Equal([SchemeA, SchemeB], schemeCommands.Select(c => (Guid)c.ParameterValue("SchemeId")!));
        Assert.Equal("17152", schemeCommands[0].ParameterValue("DistributionReference"));
        Assert.Equal(string.Empty, schemeCommands[0].ParameterValue("DistributionReferenceSuffix"));
        Assert.Equal(false, schemeCommands[0].ParameterValue("IsCancelled"));
        Assert.Equal(true, schemeCommands[1].ParameterValue("IsCancelled"));

        var participantCommands = connection.ExecutedCommands.Where(c => c.CommandText == InsertParticipantSql).ToList();
        Assert.Equal(
            [schemeCommands[0].ParameterValue("MonthlyDistributionSchemeId"), schemeCommands[0].ParameterValue("MonthlyDistributionSchemeId"), schemeCommands[1].ParameterValue("MonthlyDistributionSchemeId")],
            participantCommands.Select(c => c.ParameterValue("MonthlyDistributionSchemeId")));
        Assert.All(participantCommands, c => Assert.Equal(false, c.ParameterValue("AdditionalParticipant")));
    }

    [Fact]
    public async Task InitialiseMonthAsync_NoSchemesToDistribute_InsertsJustTheMonth()
    {
        var (repository, connection) = CreateRepository();
        var header = ExistsResponse(Guid.Empty);
        connection.RespondToQuery(ExistsSql, header);
        var empty = new DataSet();
        var schemes = new DataTable();
        schemes.Columns.Add("fldSchemeId", typeof(Guid));
        var participants = new DataTable();
        participants.Columns.Add("SchemeId", typeof(Guid));
        empty.Tables.Add(schemes);
        empty.Tables.Add(participants);
        connection.RespondToQuery(SetupSql, empty);
        connection.RespondToNonQuery(InsertMonthSql, 1);

        var result = await repository.InitialiseMonthAsync(2026, 5, Planner([], []));

        Assert.True(result);
        Assert.Equal([ExistsSql, SetupSql, InsertMonthSql], connection.ExecutedCommands.Select(c => c.CommandText));
        Assert.True(connection.LastTransaction!.Committed);
    }
}
