using System.Data;
using PTL.Data.Distribution;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;

namespace PTL.Data.Tests.Distribution;

// Monthly Distributions - Tabulations persistence read - legacy ResultsTabulation.MonthlyDistribution.DataPortal_Fetch.
public class DistributionRepositoryTabulationsTests
{
    public DistributionRepositoryTabulationsTests() => DapperColumnMappings.Register();

    private static readonly Guid MonthlyDistributionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const string TabulationsSql =
        "EXEC dbo.spgMonthlyDistributionInfoForTabulationsById @MonthlyDistributionId = @MonthlyDistributionId";

    private const string AssessmentStatusSql =
        "EXEC dbo.spgAssessmentStatus @MonthlyDistributionSchemeId = @MonthlyDistributionSchemeId";

    private static (DistributionRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        return (new DistributionRepository(new FakeDbConnectionFactory(connection)), connection);
    }

    private static DataTable HeaderTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionId", typeof(Guid));
        table.Columns.Add("fldYearId", typeof(int));
        table.Columns.Add("fldMonthId", typeof(int));
        return table;
    }

    private static DataTable SchemeTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldSchemeName", typeof(string));
        table.Columns.Add("fldSchemeIdentifier", typeof(string));
        table.Columns.Add("fldDistributionReference", typeof(string));
        table.Columns.Add("fldDistributionReferenceSuffix", typeof(string));
        table.Columns.Add("fldScheduleCode", typeof(string));
        table.Columns.Add("fldResultsIssueTargetDate", typeof(DateTime));
        table.Columns.Add("fldDeadlineDate", typeof(DateTime));
        table.Columns.Add("fldTestConsultant1Name", typeof(string));
        table.Columns.Add("fldTestConsultant2Name", typeof(string));
        table.Columns.Add("fldTestConsultant3Name", typeof(string));
        table.Columns.Add("fldIsCommentSubmitted", typeof(bool));
        table.Columns.Add("fldTestConsultantTabulationId", typeof(Guid));
        table.Columns.Add("fldEmailState", typeof(int));
        table.Columns.Add("fldIsReadyForTestConsultant", typeof(DateTime));
        table.Columns.Add("fldIsReadyForResultsSignOff", typeof(DateTime));
        table.Columns.Add("fldIsReadyForAssessor", typeof(DateTime));
        table.Columns.Add("fldAssessor1Name", typeof(string));
        table.Columns.Add("fldAssessor2Name", typeof(string));
        table.Columns.Add("fldAssessor3Name", typeof(string));
        table.Columns.Add("fldAssessor4Name", typeof(string));
        table.Columns.Add("fldHasIntendedResults", typeof(bool));
        table.Columns.Add("fldRequiresAssessment", typeof(bool));
        return table;
    }

    private static void AddScheme(DataTable table, Guid id, bool requiresAssessment) =>
        table.Rows.Add(id, "Brucella", "BT", "D1", "a", "S1", new DateTime(2026, 6, 30), new DateTime(2026, 5, 20),
            DBNull.Value, DBNull.Value, DBNull.Value, false, DBNull.Value, 0, DBNull.Value, DBNull.Value, DBNull.Value,
            DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, true, requiresAssessment);

    private static DataTable TabulationTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldMonthlyDistributionSchemeId", typeof(Guid));
        table.Columns.Add("fldDistributionTabulationId", typeof(Guid));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldPublishState", typeof(int));
        table.Columns.Add("fldPublishCount", typeof(int));
        return table;
    }

    private static DataTable StatusTable()
    {
        var table = new DataTable();
        table.Columns.Add("fldAssessmentSubmitted", typeof(DateTime));
        table.Columns.Add("fldSubmissionDate", typeof(DateTime));
        table.Columns.Add("fldNonSubmissionDate", typeof(DateTime));
        table.Columns.Add("fldNonCapableDate", typeof(DateTime));
        return table;
    }

    private static DataSet ResultSets(DataTable schemes, DataTable tabulations)
    {
        var header = HeaderTable();
        header.Rows.Add(MonthlyDistributionId, 2026, 5);
        var dataSet = new DataSet();
        dataSet.Tables.Add(header);
        dataSet.Tables.Add(schemes);
        dataSet.Tables.Add(tabulations);
        return dataSet;
    }

    [Fact]
    public async Task GetTabulationsAsync_NoSchemes_ReturnsEmptyListAndReadsNoAssessmentStatus()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(TabulationsSql, ResultSets(SchemeTable(), TabulationTable()));

        var result = await repository.GetTabulationsAsync(MonthlyDistributionId);

        Assert.Empty(result);
        var query = Assert.Single(connection.ExecutedCommands);
        Assert.Equal(TabulationsSql, query.CommandText);
        Assert.Equal(MonthlyDistributionId, query.ParameterValue("MonthlyDistributionId"));
    }

    [Fact]
    public async Task GetTabulationsAsync_MapsEverySchemeColumn()
    {
        var (repository, connection) = CreateRepository();
        var schemeId = Guid.NewGuid();
        var consultantTab = Guid.NewGuid();
        var schemes = SchemeTable();
        schemes.Rows.Add(schemeId, "Brucella", "BT", "D1", "a", "S1", new DateTime(2026, 6, 30), new DateTime(2026, 5, 20),
            "Dr A", "Dr B", "Dr C", true, consultantTab, 2, new DateTime(2026, 5, 21), new DateTime(2026, 5, 22), new DateTime(2026, 5, 23),
            "As 1", "As 2", "As 3", "As 4", true, false);
        connection.RespondToQuery(TabulationsSql, ResultSets(schemes, TabulationTable()));

        var scheme = Assert.Single(await repository.GetTabulationsAsync(MonthlyDistributionId));

        Assert.Equal(schemeId, scheme.MonthlyDistributionSchemeId);
        Assert.Equal(("Brucella", "BT"), (scheme.SchemeName, scheme.SchemeIdentifier));
        Assert.Equal(("D1", "a", "S1"), (scheme.DistributionReference, scheme.DistributionReferenceSuffix, scheme.ScheduleCode));
        Assert.Equal(new DateTime(2026, 6, 30), scheme.ResultsIssueTargetDate);
        Assert.Equal(new DateTime(2026, 5, 20), scheme.DeadlineDate);
        Assert.Equal(("Dr A", "Dr B", "Dr C"), (scheme.TestConsultant1Name, scheme.TestConsultant2Name, scheme.TestConsultant3Name));
        Assert.True(scheme.IsCommentSubmitted);
        Assert.Equal(consultantTab, scheme.TestConsultantTabulationId);
        Assert.Equal(2, scheme.EmailState);
        Assert.Equal(new DateTime(2026, 5, 21), scheme.IsReadyForTestConsultant);
        Assert.Equal(new DateTime(2026, 5, 22), scheme.IsReadyForResultsSignOff);
        Assert.Equal(new DateTime(2026, 5, 23), scheme.IsReadyForAssessor);
        Assert.Equal(("As 1", "As 2", "As 3", "As 4"), (scheme.Assessor1Name, scheme.Assessor2Name, scheme.Assessor3Name, scheme.Assessor4Name));
        Assert.True(scheme.HasIntendedResults);
        Assert.False(scheme.RequiresAssessment);
    }

    [Fact]
    public async Task GetTabulationsAsync_NullColumns_MapToNullableDefaults()
    {
        var (repository, connection) = CreateRepository();
        var schemes = SchemeTable();
        AddScheme(schemes, Guid.NewGuid(), requiresAssessment: false);
        connection.RespondToQuery(TabulationsSql, ResultSets(schemes, TabulationTable()));

        var scheme = Assert.Single(await repository.GetTabulationsAsync(MonthlyDistributionId));

        Assert.Null(scheme.TestConsultant1Name);
        Assert.Null(scheme.Assessor4Name);
        Assert.Null(scheme.TestConsultantTabulationId);
        Assert.Null(scheme.IsReadyForTestConsultant);
        Assert.Null(scheme.IsReadyForResultsSignOff);
        Assert.Null(scheme.IsReadyForAssessor);
        Assert.Empty(scheme.Tabulations);
    }

    [Fact]
    public async Task GetTabulationsAsync_AttachesTabulationsToTheirSchemeAndIgnoresUnknownSchemes()
    {
        var (repository, connection) = CreateRepository();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var tabOne = Guid.NewGuid();
        var tabTwo = Guid.NewGuid();
        var schemes = SchemeTable();
        AddScheme(schemes, first, requiresAssessment: false);
        AddScheme(schemes, second, requiresAssessment: false);
        var tabulations = TabulationTable();
        tabulations.Rows.Add(first, tabOne, "Summary", 1, 3);
        tabulations.Rows.Add(first, tabTwo, "Detail", 0, 0);
        tabulations.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), "Orphan", 0, 0);
        connection.RespondToQuery(TabulationsSql, ResultSets(schemes, tabulations));

        var result = await repository.GetTabulationsAsync(MonthlyDistributionId);

        var withTabulations = result.Single(s => s.MonthlyDistributionSchemeId == first);
        Assert.Equal(["Summary", "Detail"], withTabulations.Tabulations.Select(t => t.Name));
        var summary = withTabulations.Tabulations[0];
        Assert.Equal((first, tabOne, 1, 3), (summary.MonthlyDistributionSchemeId, summary.TabulationId, summary.PublishState, summary.PublishCount));
        Assert.Empty(result.Single(s => s.MonthlyDistributionSchemeId == second).Tabulations);
        Assert.DoesNotContain(result.SelectMany(s => s.Tabulations), t => t.Name == "Orphan");
    }

    [Fact]
    public async Task GetTabulationsAsync_ReadsAssessmentStatusOnlyForSchemesRequiringAssessment()
    {
        var (repository, connection) = CreateRepository();
        var assessed = Guid.NewGuid();
        var plain = Guid.NewGuid();
        var schemes = SchemeTable();
        AddScheme(schemes, plain, requiresAssessment: false);
        AddScheme(schemes, assessed, requiresAssessment: true);
        connection.RespondToQuery(TabulationsSql, ResultSets(schemes, TabulationTable()));
        var statuses = StatusTable();
        statuses.Rows.Add(new DateTime(2026, 5, 25), new DateTime(2026, 5, 10), DBNull.Value, DBNull.Value);
        statuses.Rows.Add(DBNull.Value, DBNull.Value, new DateTime(2026, 5, 11), DBNull.Value);
        connection.RespondToQuery(AssessmentStatusSql, statuses);

        var result = await repository.GetTabulationsAsync(MonthlyDistributionId);

        var statusCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == AssessmentStatusSql);
        Assert.Equal(assessed, statusCommand.ParameterValue("MonthlyDistributionSchemeId"));
        Assert.Empty(result.Single(s => s.MonthlyDistributionSchemeId == plain).AssessmentStatuses);

        var rows = result.Single(s => s.MonthlyDistributionSchemeId == assessed).AssessmentStatuses;
        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateTime(2026, 5, 25), rows[0].AssessmentSubmitted);
        Assert.Equal(new DateTime(2026, 5, 10), rows[0].SubmissionDate);
        Assert.Null(rows[0].NonSubmissionDate);
        Assert.Null(rows[1].AssessmentSubmitted);
        Assert.Equal(new DateTime(2026, 5, 11), rows[1].NonSubmissionDate);
        Assert.Null(rows[1].NonCapableDate);
    }

    [Fact]
    public async Task GetTabulationsAsync_RunsAssessmentStatusOncePerAssessmentSchemeWithItsOwnId()
    {
        var (repository, connection) = CreateRepository();
        var one = Guid.NewGuid();
        var two = Guid.NewGuid();
        var schemes = SchemeTable();
        AddScheme(schemes, one, requiresAssessment: true);
        AddScheme(schemes, two, requiresAssessment: true);
        connection.RespondToQuery(TabulationsSql, ResultSets(schemes, TabulationTable()));
        connection.RespondToQuery(AssessmentStatusSql, command =>
        {
            var table = StatusTable();
            if ((Guid)command.ParameterValue("MonthlyDistributionSchemeId")! == one)
            {
                table.Rows.Add(new DateTime(2026, 5, 25), new DateTime(2026, 5, 10), DBNull.Value, DBNull.Value);
            }

            return table;
        });

        var result = await repository.GetTabulationsAsync(MonthlyDistributionId);

        var requested = connection.ExecutedCommands.Where(c => c.CommandText == AssessmentStatusSql)
            .Select(c => (Guid)c.ParameterValue("MonthlyDistributionSchemeId")!).ToList();
        Assert.Equal([one, two], requested);
        Assert.Single(result.Single(s => s.MonthlyDistributionSchemeId == one).AssessmentStatuses);
        Assert.Empty(result.Single(s => s.MonthlyDistributionSchemeId == two).AssessmentStatuses);
    }
}
