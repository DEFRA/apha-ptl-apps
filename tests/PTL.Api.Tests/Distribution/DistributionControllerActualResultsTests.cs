using Microsoft.AspNetCore.Mvc;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// GET/PUT api/distributions/schemes/{mdsId}/participants/{participantId}/actual-results.
public class DistributionControllerActualResultsTests
{
    private readonly ActualResultsScenario _s = new();

    private static DistributionController CreateController(FakeDistributionRepository repository, string? userName = null)
    {
        var controller = new DistributionController(
            new DistributionService(repository),
            new SampleNumbersService(repository, new FakeSchemeService()),
            new IntendedResultsService(repository),
            new DistributionParticipantsService(repository),
            new PreparationService(repository),
            new ResultsReceiptService(repository, TimeProvider.System),
            new TabulationsService(repository, TimeProvider.System),
            new ActualResultsService(repository, new LocalClock(new DateTime(2026, 6, 15, 12, 0, 0))),
            new FakePackagingService(),
            new FakePackagingOutputService(),
            new PTL.Api.Tests.Contract.FakeAuditActorResolver(userName ?? "Guest"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributionController>.Instance);

        return controller;
    }

    private static ActualResultsSaveRequest Request(ActualResultsScenario s, string? auditWhy = null, string count = "12") =>
        new(
            [
                new ActualResultsSampleValueRequest(s.CultureS1Growth, "Positive"),
                new ActualResultsSampleValueRequest(s.CultureS1Count, count),
                new ActualResultsSampleValueRequest(s.CultureS2Growth, "Negative"),
                new ActualResultsSampleValueRequest(s.CultureS2Count, "7"),
                new ActualResultsSampleValueRequest(s.SerologyS1Titre, "5"),
            ],
            [new ActualResultsMethodItemRequest(s.MediumMethod, "Broth"), new ActualResultsMethodItemRequest(s.IncubationMethod, "37C")],
            auditWhy);

    private Task<ActionResult<ActualResultsSaveResult>> SaveAsync(DistributionController controller, ActualResultsSaveRequest request) =>
        controller.SaveActualResults(_s.SchemeId, _s.ParticipantId, request, CancellationToken.None);

    [Fact]
    public async Task GetActualResults_ParticipantNotFound_ReturnsNotFound()
    {
        var result = await CreateController(new FakeDistributionRepository())
            .GetActualResults(_s.SchemeId, _s.ParticipantId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetActualResults_ReturnsTheMappedPageIncludingNestedTestsItemsAndSamples()
    {
        var repository = _s.Seed(_s.FullyStored());

        var result = await CreateController(repository).GetActualResults(_s.SchemeId, _s.ParticipantId, CancellationToken.None);

        var response = Assert.IsType<ActualResultsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal((_s.ParticipantId, _s.SchemeId), (response.DistributionParticipantId, response.MonthlyDistributionSchemeId));
        Assert.Equal("D1/S1 BT: Brucella", response.Heading);
        Assert.Equal("Results Received for D1/S1 BT: Brucella", response.AuditObjectName);
        Assert.Equal(ActualResultsScenario.Deadline, response.DeadlineDate);
        Assert.False(response.IsNew);
        Assert.Equal(["Serology", "Culture"], response.Tests.Select(t => t.TestName));

        var culture = response.Tests.Single(t => t.TestId == _s.CultureTest);
        Assert.Equal(
            [new ActualResultsResultItemResponse(_s.GrowthItem, "Growth", "Positive|Negative", "Positive or Negative"),
             new ActualResultsResultItemResponse(_s.CountItem, "Count", "^\\d+$", "Whole number")],
            culture.ResultItems);
        Assert.Equal(
            [new ActualResultsMethodItemResponse(_s.MediumMethod, "Medium", "Agar|Broth", "Agar or Broth", "Broth"),
             new ActualResultsMethodItemResponse(_s.IncubationMethod, "Incubation", "^.+$", "Text", "37C")],
            culture.MethodItems);
        var sample = culture.Samples[0];
        Assert.Equal((_s.Sample1, "S001"), (sample.SampleId, sample.SampleNumber));
        Assert.Equal(
            [new ActualResultsSampleValueResponse(_s.CultureS1Growth, _s.GrowthItem, "Positive"),
             new ActualResultsSampleValueResponse(_s.CultureS1Count, _s.CountItem, "12")],
            sample.Values);
    }

    [Fact]
    public async Task GetActualResults_NewSet_FlagsIsNew()
    {
        var repository = _s.Seed(ActualResultsScenario.Untouched());

        var result = await CreateController(repository).GetActualResults(_s.SchemeId, _s.ParticipantId, CancellationToken.None);

        Assert.True(Assert.IsType<ActualResultsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).IsNew);
    }

    [Fact]
    public async Task SaveActualResults_ParticipantNotFound_ReturnsNotFound()
    {
        var result = await SaveAsync(CreateController(new FakeDistributionRepository()), Request(_s));

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SaveActualResults_NewSet_ReturnsSuccessAndPassesTheUserName()
    {
        var repository = _s.Seed(ActualResultsScenario.Untouched());

        var result = await SaveAsync(CreateController(repository, "APHA\\jbloggs"), Request(_s));

        var body = Assert.IsType<ActualResultsSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(body.Success);
        Assert.False(body.DeadlinePassed);
        Assert.False(body.AuditReasonRequired);
        Assert.Empty(body.FieldErrorsByItemId);
        Assert.Null(body.AuditObjectName);
        var write = Assert.Single(repository.SavedActualResults);
        Assert.Equal("APHA\\jbloggs", write.AuditWho);
        Assert.True(write.IsNew);
        Assert.Equal(8, write.SampleItems.Count);
        Assert.Equal(3, write.MethodItems.Count);
    }

    [Fact]
    public async Task SaveActualResults_NoAuthenticatedUser_FallsBackToGuest()
    {
        var repository = _s.Seed(ActualResultsScenario.Untouched());

        await SaveAsync(CreateController(repository), Request(_s));

        Assert.Equal("Guest", Assert.Single(repository.SavedActualResults).AuditWho);
    }

    [Fact]
    public async Task SaveActualResults_ExistingSetWithReason_PassesTheReasonThrough()
    {
        var repository = _s.Seed(_s.FullyStored());

        var result = await SaveAsync(CreateController(repository, "tester"), Request(_s, auditWhy: "Lab resubmitted", count: "13"));

        Assert.True(Assert.IsType<ActualResultsSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value).Success);
        var write = Assert.Single(repository.SavedActualResults);
        Assert.Equal("Lab resubmitted", write.AuditWhy);
        Assert.False(write.IsNew);
    }

    [Fact]
    public async Task SaveActualResults_ExistingSetWithNullReason_RequiresAReasonAndReturnsTheAuditObjectName()
    {
        var repository = _s.Seed(_s.FullyStored());

        var result = await SaveAsync(CreateController(repository), Request(_s, auditWhy: null, count: "13"));

        var body = Assert.IsType<ActualResultsSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(body.Success);
        Assert.True(body.AuditReasonRequired);
        Assert.False(body.DeadlinePassed);
        Assert.Equal("Results Received for D1/S1 BT: Brucella", body.AuditObjectName);
        Assert.Empty(repository.SavedActualResults);
    }

    [Fact]
    public async Task SaveActualResults_InvalidValue_ReturnsFieldErrorsKeyedByItemId()
    {
        var repository = _s.Seed(_s.FullyStored());

        var result = await SaveAsync(CreateController(repository), Request(_s, auditWhy: "why", count: "twelve"));

        var body = Assert.IsType<ActualResultsSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(body.Success);
        Assert.False(body.DeadlinePassed);
        Assert.False(body.AuditReasonRequired);
        Assert.Equal("Input must be of format: Whole number", Assert.Single(body.FieldErrorsByItemId).Value);
        Assert.Contains(_s.CultureS1Count, body.FieldErrorsByItemId.Keys);
        Assert.Empty(repository.SavedActualResults);
    }

    [Fact]
    public async Task SaveActualResults_AfterTheDeadline_ReturnsDeadlinePassed()
    {
        var repository = _s.Seed(_s.FullyStored(deadline: new DateTime(2026, 6, 1)));

        var result = await SaveAsync(CreateController(repository), Request(_s, auditWhy: "why", count: "13"));

        var body = Assert.IsType<ActualResultsSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(body.Success);
        Assert.True(body.DeadlinePassed);
        Assert.Empty(repository.SavedActualResults);
    }

    [Fact]
    public async Task SaveActualResults_NothingChanged_IsReportedAsSuccessWithoutWriting()
    {
        var repository = _s.Seed(_s.FullyStored());

        var result = await SaveAsync(CreateController(repository), Request(_s));

        Assert.True(Assert.IsType<ActualResultsSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value).Success);
        Assert.Empty(repository.SavedActualResults);
    }
}
