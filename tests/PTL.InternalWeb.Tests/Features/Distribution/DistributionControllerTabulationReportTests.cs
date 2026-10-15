using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.InternalWeb.Features.Distribution;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Distribution;

// Tabulation, Issued Tabulations, Issued Tabulation, (Issued) Assessment Results and the
// tabulation attachment - legacy Scheduling/Tabulation.aspx, PublishedTabulations.aspx,
// PublishedTabulation.aspx, AssessmentResults.aspx, PublishedAssessmentResults.aspx, Attachment.aspx.
public class DistributionControllerTabulationReportTests
{
    private static readonly Guid TabulationId = Guid.NewGuid();
    private static readonly Guid PublishedId = Guid.NewGuid();
    private static readonly Guid TestId = Guid.NewGuid();

    private static DistributionController CreateController(FakeDistributionApiClient apiClient) =>
        new(apiClient) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    internal static TabulationReportResponse Report(bool published = false) =>
        new(
            TabulationId, published, TabulationId, "FINAL REPORT for D1a/S1 BT: Brucella", "Summary", "Date Of Distribution: 03/05/2026",
            "", "", "", "Comments :", false, null, [], Guid.Empty, [], Guid.Empty, [], null, null,
            new TabulationPrintResponse([], [], []));

    private static AssessmentResultsResponse Results(params AssessmentScoreFrequencyResponse[] scores) =>
        new(true, ["Bar chart shows the frequency of assessment scores"], "Assessment Score", "Frequency", scores);

    [Fact]
    public async Task Tabulation_ApiReturnsNull_ReturnsNotFound()
    {
        var apiClient = new FakeDistributionApiClient();

        var result = await CreateController(apiClient).Tabulation(2026, 5, TabulationId, null, null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal([(TabulationId, Guid.Empty, Guid.Empty)], apiClient.RequestedTabulations);
    }

    [Fact]
    public async Task Tabulation_PassesFiltersAndMapsTheViewModel()
    {
        var participant = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var report = Report();
        var apiClient = new FakeDistributionApiClient { TabulationReport = report };

        var result = await CreateController(apiClient).Tabulation(2026, 5, TabulationId, participant, viewer, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        var model = Assert.IsType<TabulationReportViewModel>(view.Model);
        Assert.Equal((2026, 5, TabulationId, (Guid?)null), (model.YearId, model.MonthId, model.TabulationId, model.PublishedTabulationId));
        Assert.Same(report, model.Report);
        Assert.Equal([(TabulationId, participant, viewer)], apiClient.RequestedTabulations);
    }

    [Fact]
    public async Task PublishedTabulations_ApiReturnsNull_ReturnsNotFound()
    {
        var result = await CreateController(new FakeDistributionApiClient()).PublishedTabulations(2026, 5, TabulationId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PublishedTabulations_MapsTheViewModel()
    {
        var page = new PublishedTabulationsResponse(TabulationId, "D1a/S1 BT: Brucella", "Summary", []);
        var apiClient = new FakeDistributionApiClient { PublishedTabulations = page };

        var result = await CreateController(apiClient).PublishedTabulations(2026, 5, TabulationId, CancellationToken.None);

        var model = Assert.IsType<PublishedTabulationsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal((2026, 5, TabulationId), (model.YearId, model.MonthId, model.TabulationId));
        Assert.Same(page, model.Page);
    }

    [Fact]
    public async Task PublishedTabulation_ApiReturnsNull_ReturnsNotFound()
    {
        var apiClient = new FakeDistributionApiClient();

        var result = await CreateController(apiClient).PublishedTabulation(2026, 5, TabulationId, PublishedId, null, null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal([(PublishedId, Guid.Empty, Guid.Empty)], apiClient.RequestedTabulations);
    }

    [Fact]
    public async Task PublishedTabulation_RequestsTheIssuedCopyAndKeepsTheDraftTabulationForBack()
    {
        var viewer = Guid.NewGuid();
        var report = Report(published: true);
        var apiClient = new FakeDistributionApiClient { PublishedTabulationReport = report };

        var result = await CreateController(apiClient).PublishedTabulation(2026, 5, TabulationId, PublishedId, null, viewer, CancellationToken.None);

        var model = Assert.IsType<TabulationReportViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal((2026, 5, TabulationId, (Guid?)PublishedId), (model.YearId, model.MonthId, model.TabulationId, model.PublishedTabulationId));
        Assert.Same(report, model.Report);
        Assert.Equal([(PublishedId, Guid.Empty, viewer)], apiClient.RequestedTabulations);
    }

    [Fact]
    public async Task TabulationAssessmentResults_ApiReturnsNull_ReturnsNotFound()
    {
        var apiClient = new FakeDistributionApiClient();

        var result = await CreateController(apiClient).TabulationAssessmentResults(2026, 5, TabulationId, TestId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal([(TabulationId, TestId, false)], apiClient.RequestedAssessmentResults);
    }

    [Fact]
    public async Task TabulationAssessmentResults_RendersTheSharedViewForTheDraft()
    {
        var results = Results();
        var apiClient = new FakeDistributionApiClient { AssessmentResults = results };

        var result = await CreateController(apiClient).TabulationAssessmentResults(2026, 5, TabulationId, TestId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("AssessmentResults", view.ViewName);
        var model = Assert.IsType<AssessmentResultsViewModel>(view.Model);
        Assert.Equal((2026, 5, TabulationId, (Guid?)null, TestId), (model.YearId, model.MonthId, model.TabulationId, model.PublishedTabulationId, model.TestId));
        Assert.Same(results, model.Results);
    }

    [Fact]
    public async Task PublishedAssessmentResults_ApiReturnsNull_ReturnsNotFound()
    {
        var apiClient = new FakeDistributionApiClient();

        var result = await CreateController(apiClient).PublishedAssessmentResults(2026, 5, TabulationId, PublishedId, TestId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal([(PublishedId, TestId, true)], apiClient.RequestedAssessmentResults);
    }

    [Fact]
    public async Task PublishedAssessmentResults_RendersTheSharedViewForTheIssuedCopy()
    {
        var apiClient = new FakeDistributionApiClient { AssessmentResults = Results() };

        var result = await CreateController(apiClient).PublishedAssessmentResults(2026, 5, TabulationId, PublishedId, TestId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("AssessmentResults", view.ViewName);
        var model = Assert.IsType<AssessmentResultsViewModel>(view.Model);
        Assert.Equal((TabulationId, (Guid?)PublishedId, TestId), (model.TabulationId, model.PublishedTabulationId, model.TestId));
    }

    [Fact]
    public void AssessmentResultsViewModel_MaxFrequency_IsTheTallestBarAndAtLeastOne()
    {
        AssessmentResultsViewModel Model(params AssessmentScoreFrequencyResponse[] scores) =>
            new() { YearId = 2026, MonthId = 5, TabulationId = TabulationId, TestId = TestId, Results = Results(scores) };

        Assert.Equal(1, Model().MaxFrequency);
        Assert.Equal(1, Model(new AssessmentScoreFrequencyResponse(1, "1 Fail", 0)).MaxFrequency);
        Assert.Equal(7, Model(new(1, "1 Fail", 2), new(2, "2 Borderline Fail", 7), new(0, "Not Submitted", 3)).MaxFrequency);
    }

    [Fact]
    public async Task TabulationAttachment_Missing_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient());

        Assert.IsType<NotFoundResult>(await controller.TabulationAttachment(TabulationId, Guid.NewGuid(), CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.PublishedTabulationAttachment(PublishedId, Guid.NewGuid(), CancellationToken.None));
        Assert.False(controller.Response.Headers.ContainsKey("Content-Disposition"));
    }

    [Fact]
    public async Task TabulationAttachment_PassesTheApisContentTypeAndDispositionThrough()
    {
        var apiClient = new FakeDistributionApiClient
        {
            TabulationAttachment = new TabulationAttachmentFile([1, 2], "application/pdf", "inline; filename=notes.pdf"),
        };
        var controller = CreateController(apiClient);

        var file = Assert.IsType<FileContentResult>(await controller.TabulationAttachment(TabulationId, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal([1, 2], file.FileContents);
        Assert.Equal("inline; filename=notes.pdf", controller.Response.Headers.ContentDisposition.ToString());
    }

    [Fact]
    public async Task PublishedTabulationAttachment_PassesTheApisContentTypeAndDispositionThrough()
    {
        var apiClient = new FakeDistributionApiClient
        {
            TabulationAttachment = new TabulationAttachmentFile([3], "application/octet-stream", "attachment; filename=page.html"),
        };
        var controller = CreateController(apiClient);

        var file = Assert.IsType<FileContentResult>(await controller.PublishedTabulationAttachment(PublishedId, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("application/octet-stream", file.ContentType);
        Assert.Equal("attachment; filename=page.html", controller.Response.Headers.ContentDisposition.ToString());
    }
}
