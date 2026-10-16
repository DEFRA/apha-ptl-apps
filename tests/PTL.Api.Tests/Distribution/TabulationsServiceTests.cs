using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TabulationsTestData;

namespace PTL.Api.Tests.Distribution;

// Monthly Distributions - Tabulations read model (legacy Scheduling/Tabulations.aspx).
public class TabulationsServiceTests
{
    private static readonly Guid MonthlyDistributionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeDistributionRepository _repository = new();
    private readonly TabulationsService _service;

    public TabulationsServiceTests() => _service = new TabulationsService(_repository, Clock());

    private void SeedMonth(params TabulationsSchemeEntity[] schemes)
    {
        _repository.Schedules[(2026, 5)] = (MonthlyDistributionId, []);
        _repository.Tabulations[MonthlyDistributionId] = schemes;
    }

    [Fact]
    public async Task Get_MonthNotInitialised_ReturnsNullWithoutReadingTabulations()
    {
        var result = await _service.GetTabulationsAsync(2026, 5);

        Assert.Null(result);
        Assert.Empty(_repository.TabulationRequests);
    }

    [Fact]
    public async Task Get_InitialisedMonthWithNoSchemes_ReturnsEmptyPage()
    {
        SeedMonth();

        var result = await _service.GetTabulationsAsync(2026, 5);

        Assert.NotNull(result);
        Assert.Equal((2026, 5), (result!.YearId, result.MonthId));
        Assert.Empty(result.Schemes);
    }

    [Fact]
    public async Task Get_PassesTheMonthlyDistributionIdToTheRepository()
    {
        SeedMonth(NewScheme());

        await _service.GetTabulationsAsync(2026, 5);

        Assert.Equal([MonthlyDistributionId], _repository.TabulationRequests);
    }

    [Fact]
    public async Task Get_MapsSchemeNamesReferencesAndDates()
    {
        var scheme = NewScheme(s =>
        {
            s.DeadlineDate = new DateTime(2026, 5, 20);
            s.ResultsIssueTargetDate = new DateTime(2026, 6, 30);
        });
        SeedMonth(scheme);

        var result = await _service.GetTabulationsAsync(2026, 5);

        var mapped = Assert.Single(result!.Schemes);
        Assert.Equal(scheme.MonthlyDistributionSchemeId, mapped.MonthlyDistributionSchemeId);
        Assert.Equal("BT: Brucella", mapped.SchemeNameFull);
        Assert.Equal("D1a/S1", mapped.DistributionReferenceFull);
        Assert.Equal(new DateTime(2026, 5, 20), mapped.DeadlineDate);
        Assert.Equal(new DateTime(2026, 6, 30), mapped.ResultsIssueTargetDate);
        Assert.False(mapped.RequiresAssessment);
    }

    [Fact]
    public async Task Get_NonAssessmentScheme_ListsThreeTestConsultantSlotsWithBlankNames()
    {
        SeedMonth(NewScheme(s =>
        {
            s.TestConsultant1Name = "Dr A";
            s.TestConsultant2Name = null;
            s.TestConsultant3Name = "Dr C";
            s.Assessor1Name = "Ignored Assessor";
        }));

        var result = await _service.GetTabulationsAsync(2026, 5);

        Assert.Equal(["Dr A", "", "Dr C"], result!.Schemes[0].Consultants);
    }

    [Fact]
    public async Task Get_AssessmentScheme_ListsFourAssessorSlotsWithBlankNames()
    {
        SeedMonth(NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.Assessor1Name = "A1";
            s.Assessor2Name = null;
            s.Assessor3Name = "A3";
            s.Assessor4Name = null;
            s.TestConsultant1Name = "Ignored TC";
        }));

        var result = await _service.GetTabulationsAsync(2026, 5);

        Assert.Equal(["A1", "", "A3", ""], result!.Schemes[0].Consultants);
    }

    [Fact]
    public async Task Get_MarksOnlyTheTestConsultantTabulationAndCarriesThePublishCount()
    {
        var consultantTab = Guid.NewGuid();
        SeedMonth(NewScheme(s =>
        {
            s.TestConsultantTabulationId = consultantTab;
            s.Tabulations = [Tabulation("Other", publishCount: 0), Tabulation("Consultant", publishCount: 2, id: consultantTab)];
        }));

        var result = await _service.GetTabulationsAsync(2026, 5);

        var rows = result!.Schemes[0].Tabulations;
        Assert.Equal(["Other", "Consultant"], rows.Select(r => r.Name));
        Assert.Equal([false, true], rows.Select(r => r.IsTestConsultant));
        Assert.Equal([0, 2], rows.Select(r => r.PublishCount));
        Assert.Equal(consultantTab, rows[1].TabulationId);
    }

    [Fact]
    public async Task Get_NoTestConsultantTabulation_MarksNone()
    {
        SeedMonth(NewScheme(s => s.Tabulations = [Tabulation("One"), Tabulation("Two")]));

        var result = await _service.GetTabulationsAsync(2026, 5);

        Assert.All(result!.Schemes[0].Tabulations, r => Assert.False(r.IsTestConsultant));
    }

    [Fact]
    public async Task Get_SchemeWithoutTabulations_HasNoRowsAndDisabledActions()
    {
        SeedMonth(NewScheme(s => s.Tabulations = []));

        var result = await _service.GetTabulationsAsync(2026, 5);

        var mapped = Assert.Single(result!.Schemes);
        Assert.Empty(mapped.Tabulations);
        Assert.False(mapped.Actions.SendToResultsSignOff.IsEnabled);
        Assert.Equal("Sample Numbers not defined", mapped.Actions.AmendResults.Hint);
    }

    [Fact]
    public async Task Get_ComputesActionsAgainstTheProvidedClock_AndKeepsSchemeOrder()
    {
        var deadlineToday = NewScheme(s => s.DeadlineDate = Today);
        var deadlinePassed = NewScheme(s => s.DeadlineDate = Today.AddDays(-1));
        SeedMonth(deadlineToday, deadlinePassed);

        var result = await _service.GetTabulationsAsync(2026, 5);

        Assert.Equal([deadlineToday.MonthlyDistributionSchemeId, deadlinePassed.MonthlyDistributionSchemeId], result!.Schemes.Select(s => s.MonthlyDistributionSchemeId));
        Assert.False(result.Schemes[0].Actions.SendToTestConsultant.IsEnabled);
        Assert.True(result.Schemes[1].Actions.SendToTestConsultant.IsEnabled);
    }

    [Fact]
    public async Task Get_AssessmentSchemeStateChange_ShowsTheNewActionStates()
    {
        var scheme = NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.AssessmentStatuses = [Status(assessmentSubmitted: false)];
            s.IsReadyForAssessor = Today.AddDays(-1);
        });
        SeedMonth(scheme);

        var awaiting = (await _service.GetTabulationsAsync(2026, 5))!.Schemes[0].Actions;
        scheme.AssessmentStatuses = [Status()];
        var assessed = (await _service.GetTabulationsAsync(2026, 5))!.Schemes[0].Actions;

        Assert.Equal("Awaiting Assessment", awaiting.SendForAssessment.Text);
        Assert.False(awaiting.SendForAssessment.IsEnabled);
        Assert.Equal("View/Enter Comments", assessed.SendForAssessment.Text);
        Assert.True(assessed.SendForAssessment.IsEnabled);
        Assert.Equal(TabulationCommand.ViewComments, assessed.SendForAssessment.Command);
    }
}
