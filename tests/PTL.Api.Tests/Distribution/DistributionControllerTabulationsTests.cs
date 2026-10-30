using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TabulationsTestData;

namespace PTL.Api.Tests.Distribution;

public class DistributionControllerTabulationsTests
{
    private static readonly Guid MonthlyDistributionId = Guid.NewGuid();

    private static DistributionController CreateController(FakeDistributionRepository repository) =>
        new(
            new DistributionService(repository),
            new SampleNumbersService(repository, new FakeSchemeService()),
            new IntendedResultsService(repository),
            new DistributionParticipantsService(repository),
            new PreparationService(repository),
            new ResultsReceiptService(repository, TimeProvider.System),
            new TabulationsService(repository, Clock()),
            new ActualResultsService(repository, TimeProvider.System),
            new ParticipantDistributionHistoryService(repository, new PTL.Api.Tests.Participant.FakeParticipantRepository(), new ActualResultsService(repository, TimeProvider.System)),
            new FakePackagingService(),
            new FakePackagingOutputService(),
            new PTL.Api.Tests.Contract.FakeAuditActorResolver("Guest"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributionController>.Instance);

    private static FakeDistributionRepository Seeded(params TabulationsSchemeEntity[] schemes)
    {
        var repository = new FakeDistributionRepository();
        repository.Schedules[(2026, 5)] = (MonthlyDistributionId, []);
        repository.Tabulations[MonthlyDistributionId] = schemes;
        return repository;
    }

    private static async Task<TabulationsResponse> GetResponse(FakeDistributionRepository repository)
    {
        var result = await CreateController(repository).GetTabulations(2026, 5, CancellationToken.None);
        return Assert.IsType<TabulationsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task GetTabulations_MonthNotInitialised_ReturnsNotFound()
    {
        var result = await CreateController(new FakeDistributionRepository()).GetTabulations(2026, 5, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetTabulations_InitialisedMonthWithoutSchemes_ReturnsEmptyList()
    {
        var response = await GetResponse(Seeded());

        Assert.Equal((2026, 5), (response.YearId, response.MonthId));
        Assert.Empty(response.Schemes);
    }

    [Fact]
    public async Task GetTabulations_MapsSchemeConsultantsAndTabulationRows()
    {
        var consultantTab = Guid.NewGuid();
        var scheme = NewScheme(s =>
        {
            s.TestConsultant1Name = "Dr A";
            s.TestConsultantTabulationId = consultantTab;
            s.DeadlineDate = new DateTime(2026, 5, 20);
            s.ResultsIssueTargetDate = new DateTime(2026, 6, 30);
            s.Tabulations = [Tabulation("Summary", publishCount: 2, id: consultantTab), Tabulation("Detail")];
        });

        var response = await GetResponse(Seeded(scheme));

        var mapped = Assert.Single(response.Schemes);
        Assert.Equal(scheme.MonthlyDistributionSchemeId, mapped.MonthlyDistributionSchemeId);
        Assert.Equal("BT: Brucella", mapped.SchemeNameFull);
        Assert.Equal("D1a/S1", mapped.DistributionReferenceFull);
        Assert.Equal(new DateTime(2026, 5, 20), mapped.DeadlineDate);
        Assert.Equal(new DateTime(2026, 6, 30), mapped.ResultsIssueTargetDate);
        Assert.False(mapped.RequiresAssessment);
        Assert.Equal(["Dr A", "", ""], mapped.Consultants);
        Assert.Equal(
            [new TabulationRowResponse(consultantTab, "Summary", true, 2), new TabulationRowResponse(mapped.Tabulations[1].TabulationId, "Detail", false, 0)],
            mapped.Tabulations);
    }

    [Fact]
    public async Task GetTabulations_MapsActionCommandAsStringAndFlags()
    {
        // Chaser sent more than 9 days ago -> enabled OVERDUE link (Test Consultant); target date passed with the
        // distribution waiting for sign-off -> red OVERDUE label (sign-off).
        var overdueComments = NewScheme(s =>
        {
            s.IsReadyForTestConsultant = Today.AddDays(-20);
            s.EmailState = (int)TabulationEmailState.ChaserEmailSent;
        });
        var overdueSignOff = NewScheme(s =>
        {
            s.IsReadyForResultsSignOff = Today.AddDays(-2);
            s.ResultsIssueTargetDate = Today.AddDays(-1);
        });

        var schemes = (await GetResponse(Seeded(overdueComments, overdueSignOff))).Schemes;
        var actions = schemes[0].Actions;
        var signOff = schemes[1].Actions.SendToResultsSignOff;

        Assert.Equal(new TabulationActionResponse("Amend Results", true, "Click to Amend the Results for this Distribution.", "None"), actions.AmendResults);
        Assert.Equal("ViewComments", actions.SendToTestConsultant.Command);
        Assert.Equal("OVERDUE", actions.SendToTestConsultant.Text);
        Assert.True(actions.SendToTestConsultant.IsEnabled);
        Assert.True(actions.SendToTestConsultant.IsOverdue);
        Assert.False(actions.SendToTestConsultant.IsLabel);
        Assert.Equal("SendForAssessment", actions.SendForAssessment.Command);
        Assert.Equal("SendToResultsSignOff", signOff.Command);
        Assert.Equal("OVERDUE", signOff.Text);
        Assert.False(signOff.IsEnabled);
        Assert.True(signOff.IsOverdue);
        Assert.True(signOff.IsLabel);
    }

    [Fact]
    public async Task GetTabulations_AssessmentScheme_ReturnsAssessorsAndSampleNumbersHintWhenNoTabulations()
    {
        var scheme = NewScheme(s =>
        {
            s.RequiresAssessment = true;
            s.Assessor1Name = "Assessor One";
            s.Tabulations = [];
        });

        var mapped = Assert.Single((await GetResponse(Seeded(scheme))).Schemes);

        Assert.True(mapped.RequiresAssessment);
        Assert.Equal(["Assessor One", "", "", ""], mapped.Consultants);
        Assert.Empty(mapped.Tabulations);
        Assert.False(mapped.Actions.SendForAssessment.IsEnabled);
        Assert.Equal("Sample Numbers not defined", mapped.Actions.SendForAssessment.Hint);
    }
}
