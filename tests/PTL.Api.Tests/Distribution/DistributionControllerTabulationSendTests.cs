using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.TabulationsTestData;

namespace PTL.Api.Tests.Distribution;

// Tabulations staged sends - POST preview / POST stage / PUT save (ITabulationSendService via [FromServices]).
public class DistributionControllerTabulationSendTests
{
    private static readonly Guid MonthlyDistributionId = Guid.NewGuid();

    private readonly FakeDistributionRepository _repository = new();

    private TabulationSendService Service => new(_repository, new FakeTabulationEmailService(), Clock());

    private DistributionController CreateController() =>
        new(
            new DistributionService(_repository),
            new SampleNumbersService(_repository, new FakeSchemeService()),
            new IntendedResultsService(_repository),
            new DistributionParticipantsService(_repository),
            new PreparationService(_repository),
            new ResultsReceiptService(_repository, TimeProvider.System),
            new TabulationsService(_repository, Clock()),
            new ActualResultsService(_repository, TimeProvider.System),
            new ParticipantDistributionHistoryService(_repository, new PTL.Api.Tests.Participant.FakeParticipantRepository(), new ActualResultsService(_repository, TimeProvider.System)),
            new FakePackagingService(),
            new FakePackagingOutputService(),
            new PTL.Api.Tests.Contract.FakeAuditActorResolver("Guest"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributionController>.Instance);

    private TabulationsSchemeEntity Seed()
    {
        var scheme = NewScheme();
        _repository.Schedules[(2026, 5)] = (MonthlyDistributionId, []);
        _repository.Tabulations[MonthlyDistributionId] = [scheme];
        return scheme;
    }

    private static TabulationsStagedRequest Staged(params TabulationStagedSendRequest[] sends) => new(sends);

    [Fact]
    public async Task PreviewTabulations_MonthMissing_ReturnsNotFound()
    {
        var result = await CreateController().PreviewTabulations(2026, 5, Staged(), Service, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task PreviewTabulations_ReturnsThePageWithTheStagedSendsApplied()
    {
        var scheme = Seed();

        var result = await CreateController().PreviewTabulations(
            2026, 5, Staged(new TabulationStagedSendRequest(scheme.MonthlyDistributionSchemeId, "SendToTestConsultant")), Service, CancellationToken.None);

        var page = Assert.IsType<TabulationsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal((2026, 5), (page.YearId, page.MonthId));
        var action = Assert.Single(page.Schemes).Actions.SendToTestConsultant;
        Assert.Equal(("Waiting to send Email 12/05/2026", false, "SendToTestConsultant"), (action.Text, action.IsEnabled, action.Command));
        Assert.Empty(_repository.TabulationSends);
    }

    [Fact]
    public async Task PreviewTabulations_NullBody_IsTreatedAsNothingStaged()
    {
        Seed();

        var result = await CreateController().PreviewTabulations(2026, 5, null!, Service, CancellationToken.None);

        var page = Assert.IsType<TabulationsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Send To Test Consultant", page.Schemes[0].Actions.SendToTestConsultant.Text);
    }

    [Fact]
    public async Task StageTabulationSend_Available_ReturnsTheConfirmation()
    {
        var scheme = Seed();

        var result = await CreateController().StageTabulationSend(
            2026, 5, new TabulationStageRequest([], scheme.MonthlyDistributionSchemeId, "SendToTestConsultant"), Service, CancellationToken.None);

        var send = Assert.IsType<TabulationSendResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(TabulationSendService.SendToTestConsultantQuestion, send.ConfirmationQuestion);
    }

    [Fact]
    public async Task StageTabulationSend_Unavailable_ReturnsNotFound()
    {
        var scheme = Seed();

        var result = await CreateController().StageTabulationSend(
            2026, 5, new TabulationStageRequest([], scheme.MonthlyDistributionSchemeId, "Bogus"), Service, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SaveTabulations_Saved_ReturnsNoContentAndWrites()
    {
        var scheme = Seed();

        var result = await CreateController().SaveTabulations(
            2026, 5, Staged(new TabulationStagedSendRequest(scheme.MonthlyDistributionSchemeId, "SendToResultsSignOff")), Service, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(scheme.MonthlyDistributionSchemeId, Assert.Single(Assert.Single(_repository.TabulationSends)).MonthlyDistributionSchemeId);
    }

    [Fact]
    public async Task SaveTabulations_NullBody_SavesNothingAndReturnsNoContent()
    {
        Seed();

        Assert.IsType<NoContentResult>(await CreateController().SaveTabulations(2026, 5, null!, Service, CancellationToken.None));
        Assert.Empty(_repository.TabulationSends);
    }

    [Fact]
    public async Task SaveTabulations_StaleSend_ReturnsConflict()
    {
        var scheme = Seed();

        var result = await CreateController().SaveTabulations(
            2026, 5, Staged(new(scheme.MonthlyDistributionSchemeId, "SendToResultsSignOff"), new(scheme.MonthlyDistributionSchemeId, "SendToResultsSignOff")),
            Service, CancellationToken.None);

        Assert.IsType<ConflictResult>(result);
        Assert.Empty(_repository.TabulationSends);
    }

    [Fact]
    public async Task SaveTabulations_MonthMissing_ReturnsConflict()
    {
        Assert.IsType<ConflictResult>(await CreateController().SaveTabulations(2026, 5, Staged(), Service, CancellationToken.None));
    }
}
