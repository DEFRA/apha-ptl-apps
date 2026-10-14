using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTL.Api.Controllers;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;
using static PTL.Api.Tests.Distribution.PackagingTestData;

namespace PTL.Api.Tests.Distribution;

// Sample Packaging endpoints of DistributionController: selection validation (mode 0-2, week 1-4
// for the two Combined options), mapping and the file/problem outcome of Print Customs.
public class DistributionControllerPackagingTests
{
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private sealed class Harness
    {
        public FakePackagingService Packaging { get; } = new();
        public FakePackagingOutputService Output { get; } = new();
        public DistributionController Controller { get; }

        public Harness()
        {
            var repository = new FakeDistributionRepository();
            Controller = new DistributionController(
                new DistributionService(repository),
                new SampleNumbersService(repository, new FakeSchemeService()),
                new IntendedResultsService(repository),
                new DistributionParticipantsService(repository),
                new PreparationService(repository),
                new ResultsReceiptService(repository, TimeProvider.System),
                new ActualResultsService(repository, TimeProvider.System),
                Packaging,
                Output,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributionController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddMvc().Services.BuildServiceProvider() },
                },
            };
        }
    }

    private static PackagingSheet Sheet() =>
        new(
            PackagingMode.ParticipantCombined,
            2,
            2026,
            5,
            [
                new PackagingBlock(
                    "key1", Part1, Day1, "L001: Lab One", "Pack info", DueToBePosted: true, ShowAllPacked: true, ShowAllPosted: false,
                    [
                        new PackagingSchemeRow(
                            Scheme1, "scheme text", true, "scheme pack", "cust pack", "ps pack", "Yes", Dp1, Day1, Day2,
                            PackagingTotalState.Selectable, PackagingTotalState.Complete,
                            [new PackagingParticipantRow(Dp2, "participant text", Day1, null, "N/A")]),
                    ]),
            ]);

    // ---------- GetPackaging ----------

    [Theory]
    [InlineData(3, 1)]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(0, 5)]
    [InlineData(0, -1)]
    [InlineData(1, 0)]
    [InlineData(1, 5)]
    public async Task GetPackaging_InvalidSelection_ReturnsValidationProblem(int mode, int week)
    {
        var h = new Harness();

        var result = await h.Controller.GetPackaging(2026, 5, mode, week, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result, exactMatch: false);
        var problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Contains("mode", problem.Errors.Keys);
        Assert.Empty(h.Packaging.SheetRequests);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 4)]
    [InlineData(1, 1)]
    [InlineData(1, 4)]
    public async Task GetPackaging_CombinedOptionsAcceptWeeksOneToFour(int mode, int week)
    {
        var h = new Harness();
        h.Packaging.Sheet = Sheet();

        var result = await h.Controller.GetPackaging(2026, 5, mode, week, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal((2026, 5, (PackagingMode)mode, week), Assert.Single(h.Packaging.SheetRequests));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(99)]
    public async Task GetPackaging_DailyIgnoresTheWeek(int week)
    {
        var h = new Harness();
        h.Packaging.Sheet = Sheet();

        var result = await h.Controller.GetPackaging(2026, 5, 2, week, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal((2026, 5, PackagingMode.Daily, 0), Assert.Single(h.Packaging.SheetRequests));
    }

    [Fact]
    public async Task GetPackaging_ServiceReturnsNull_ReturnsNotFound()
    {
        var h = new Harness();

        var result = await h.Controller.GetPackaging(2026, 5, 1, 2, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetPackaging_ReturnsTheMappedSheet()
    {
        var h = new Harness();
        h.Packaging.Sheet = Sheet();

        var result = await h.Controller.GetPackaging(2026, 5, 1, 2, CancellationToken.None);

        var response = Assert.IsType<PackagingSheetResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(1, response.Mode);
        Assert.Equal(2, response.Week);
        Assert.Equal(2026, response.YearId);
        Assert.Equal(5, response.MonthId);
        var block = Assert.Single(response.Blocks);
        Assert.Equal("key1", block.PrintKey);
        Assert.Equal(Day1, block.Date);
        Assert.Equal("L001: Lab One", block.Heading);
        Assert.Equal("Pack info", block.PackingInfo);
        Assert.True(block.DueToBePosted);
        Assert.True(block.ShowAllPacked);
        Assert.False(block.ShowAllPosted);
        var scheme = Assert.Single(block.Schemes);
        Assert.Equal(Scheme1, scheme.MonthlyDistributionSchemeId);
        Assert.Equal("scheme text", scheme.Description);
        Assert.True(scheme.PreparationComplete);
        Assert.Equal("scheme pack", scheme.PackingInfo);
        Assert.Equal("cust pack", scheme.CustomerPackingInfo);
        Assert.Equal("ps pack", scheme.PSLevelPackingInfo);
        Assert.Equal("Yes", scheme.ImportPermitStatus);
        Assert.Equal(Dp1, scheme.DistributionParticipantId);
        Assert.Equal(Day1, scheme.PackagedDate);
        Assert.Equal(Day2, scheme.PostedDate);
        Assert.Equal(2, scheme.PackedTotal);
        Assert.Equal(1, scheme.PostedTotal);
        var participant = Assert.Single(scheme.Participants);
        Assert.Equal(Dp2, participant.DistributionParticipantId);
        Assert.Equal("participant text", participant.Description);
        Assert.Equal(Day1, participant.PackagedDate);
        Assert.Null(participant.PostedDate);
        Assert.Equal("N/A", participant.ImportPermitStatus);
    }

    // ---------- SavePackaging ----------

    [Fact]
    public async Task SavePackaging_InvalidUpdate_ReturnsValidationProblemAndSavesNothing()
    {
        var h = new Harness();
        var request = new PackagingSaveRequest(
        [
            new PackagingStageUpdateRequest(1, 2, Dp1, true, false),
            new PackagingStageUpdateRequest(1, 9, Dp2, true, false),
        ]);

        var result = await h.Controller.SavePackaging(2026, 5, request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result, exactMatch: false);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Empty(h.Packaging.SavedUpdates);
    }

    [Fact]
    public async Task SavePackaging_ServiceReportsFailure_ReturnsNotFound()
    {
        var h = new Harness();
        h.Packaging.SaveOutcome = new PackagingSaveOutcome(false);

        var result = await h.Controller.SavePackaging(2026, 5, new PackagingSaveRequest([]), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SavePackaging_EmailFailures_AreReturnedToTheCaller()
    {
        var h = new Harness();
        h.Packaging.SaveOutcome = new PackagingSaveOutcome(true, ["lab@example.test (17152A/BA)"]);

        var result = await h.Controller.SavePackaging(2026, 5, new PackagingSaveRequest([]), CancellationToken.None);

        var saved = Assert.IsType<PackagingSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(saved.Success);
        Assert.Equal("lab@example.test (17152A/BA)", Assert.Single(saved.EmailFailures!));
    }

    [Fact]
    public async Task SavePackaging_Valid_PassesMappedUpdatesAndReturnsSuccess()
    {
        var h = new Harness();
        var request = new PackagingSaveRequest(
        [
            new PackagingStageUpdateRequest(0, 3, Dp1, true, false),
            new PackagingStageUpdateRequest(2, 4, Dp2, false, true),
        ]);

        var result = await h.Controller.SavePackaging(2026, 5, request, CancellationToken.None);

        Assert.True(Assert.IsType<PackagingSaveResult>(Assert.IsType<OkObjectResult>(result.Result).Value).Success);
        var updates = Assert.Single(h.Packaging.SavedUpdates);
        Assert.Equal(new PackagingStageUpdate(PackagingMode.GroupAddress, 3, Dp1, true, false), updates[0]);
        // Daily forces the week to 0.
        Assert.Equal(new PackagingStageUpdate(PackagingMode.Daily, 0, Dp2, false, true), updates[1]);
    }

    // ---------- GetPackagingLabels ----------

    [Fact]
    public async Task GetPackagingLabels_InvalidSelection_ReturnsValidationProblem()
    {
        var h = new Harness();

        var result = await h.Controller.GetPackagingLabels(2026, 5, new PackagingSelectionRequest(1, 0, ["k"]), CancellationToken.None);

        Assert.IsType<ValidationProblemDetails>(Assert.IsType<ObjectResult>(result.Result, exactMatch: false).Value);
        Assert.Empty(h.Output.LabelRequests);
    }

    [Fact]
    public async Task GetPackagingLabels_MonthNotInitialised_ReturnsNotFound()
    {
        var h = new Harness();

        var result = await h.Controller.GetPackagingLabels(2026, 5, new PackagingSelectionRequest(1, 2, ["k"]), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetPackagingLabels_ReturnsTheLabelLines()
    {
        var h = new Harness();
        h.Output.Labels = [new PackagingLabel(["Ann", "Org"]), new PackagingLabel(["Bob"])];

        var result = await h.Controller.GetPackagingLabels(2026, 5, new PackagingSelectionRequest(2, 0, ["k1", "k2"]), CancellationToken.None);

        var response = Assert.IsType<PackagingLabelsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, response.Labels.Count);
        Assert.Equal(["Ann", "Org"], response.Labels[0].Lines);
        Assert.Equal(["Bob"], response.Labels[1].Lines);
        var request = Assert.Single(h.Output.LabelRequests);
        Assert.Equal(PackagingMode.Daily, request.Mode);
        Assert.Equal(["k1", "k2"], request.Keys);
    }

    // ---------- GetPackagingCustoms ----------

    [Fact]
    public async Task GetPackagingCustoms_InvalidSelection_ReturnsValidationProblem()
    {
        var h = new Harness();

        var result = await h.Controller.GetPackagingCustoms(2026, 5, new PackagingSelectionRequest(7, 1, []), CancellationToken.None);

        Assert.IsType<ValidationProblemDetails>(Assert.IsType<ObjectResult>(result, exactMatch: false).Value);
        Assert.Empty(h.Output.CustomsRequests);
    }

    [Fact]
    public async Task GetPackagingCustoms_Failure_ReturnsBadRequestProblemWithTheReason()
    {
        var h = new Harness();
        h.Output.Customs = new PackagingCustomsOutcome(false, "Please select schemes to print", string.Empty, []);

        var result = await h.Controller.GetPackagingCustoms(2026, 5, new PackagingSelectionRequest(2, 0, []), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        Assert.Equal("Please select schemes to print", Assert.IsType<ProblemDetails>(objectResult.Value).Detail);
    }

    [Fact]
    public async Task GetPackagingCustoms_Success_ReturnsTheDocxFile()
    {
        var h = new Harness();
        h.Output.Customs = new PackagingCustomsOutcome(true, null, "Daily packaging May 2026.docx", [1, 2, 3]);

        var result = await h.Controller.GetPackagingCustoms(2026, 5, new PackagingSelectionRequest(2, 0, ["k"]), CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(DocxContentType, file.ContentType);
        Assert.Equal("Daily packaging May 2026.docx", file.FileDownloadName);
        Assert.Equal([1, 2, 3], file.FileContents);
        Assert.Equal(["k"], Assert.Single(h.Output.CustomsRequests).Keys);
    }
}
