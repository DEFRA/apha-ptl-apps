using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.ExternalWeb.Features.Tabulation;
using PTL.ExternalWeb.Infrastructure;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.Tabulation;

// Index/Report/AssessmentResults/Attachment behaviour is covered end-to-end by
// Integration/TabulationRouteSmokeTests.cs (they need a real HTTP pipeline for
// User.GetParticipantId()/GetViewerId()/GetTestConsultantId() and the rendered Razor views).
public class TabulationControllerTests
{
    [Fact]
    public async Task GeneratePdf_WhenReportExists_ReturnsPdfFile()
    {
        var api = new FakeDistributionApiClient { PublishedTabulation = Report(Guid.NewGuid()) };
        var service = CreatePdfService();
        var controller = CreateController(api, service);

        var result = await controller.GeneratePdf(Guid.NewGuid(), CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("Summary.pdf", file.FileDownloadName);
        Assert.Equal("%PDF-1.7"u8.ToArray(), file.FileContents);
    }

    [Fact]
    public async Task GeneratePdf_WhenReportMissing_ReturnsNotFound()
    {
        var controller = CreateController(new FakeDistributionApiClient(), CreatePdfService());

        var result = await controller.GeneratePdf(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GeneratePdf_WhenRenderingFails_ReturnsServiceUnavailable()
    {
        var api = new FakeDistributionApiClient { PublishedTabulation = Report(Guid.NewGuid()) };
        var service = CreatePdfService(new InvalidOperationException("boom"));
        var controller = CreateController(api, service);

        var result = await controller.GeneratePdf(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task TabulationPdfService_PassesFooterPagingAndPrintMarginsToRenderer()
    {
        var report = Report(Guid.NewGuid());
        var renderer = new FakeHtmlToPdfRenderer("%PDF-1.7"u8.ToArray());
        var service = new TabulationPdfService(
            new FakeRazorViewRenderer("<html><body>pdf</body></html>"),
            renderer,
            new FakeWebHostEnvironment());

        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var result = await service.GenerateAsync(actionContext, new TabulationReportPageViewModel(report), CancellationToken.None);

        Assert.Equal("%PDF-1.7"u8.ToArray(), result);

        var options = Assert.Single(renderer.RenderedOptions);
        Assert.NotNull(options);
        Assert.Equal("A3", options!.Format);
        Assert.True(options.Landscape);
        Assert.Equal(0.9f, options.Scale, 3);
        Assert.Equal("12mm", options.MarginTop);
        Assert.Equal("16mm", options.MarginBottom);
        Assert.Equal("8mm", options.MarginLeft);
        Assert.Equal("8mm", options.MarginRight);
        Assert.Contains("pageNumber", options.FooterTemplate, StringComparison.Ordinal);
        Assert.Contains("totalPages", options.FooterTemplate, StringComparison.Ordinal);
        Assert.Contains("D1a/S1 BT: Brucella - Summary.", options.FooterTemplate, StringComparison.Ordinal);
    }

    private static TabulationController CreateController(FakeDistributionApiClient api, ITabulationPdfService service)
    {
        var controller = new TabulationController(api, service, NullLogger<TabulationController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return controller;
    }

    private static ITabulationPdfService CreatePdfService(Exception? throwOnRender = null)
    {
        var htmlRenderer = new FakeHtmlToPdfRenderer("%PDF-1.7"u8.ToArray(), throwOnRender);
        return new TabulationPdfService(
            new FakeRazorViewRenderer("<html><body>pdf</body></html>"),
            htmlRenderer,
            new FakeWebHostEnvironment());
    }

    private static PTL.Contracts.Distribution.TabulationReportResponse Report(Guid tabulationId) => new(
        tabulationId,
        IsPublished: true,
        DistributionTabulationId: tabulationId,
        Title: "FINAL REPORT",
        TabulationName: "Summary",
        DistributionDateText: "Date Of Distribution: 01/05/2026",
        StandardTabulationTextHtml: string.Empty,
        CommentsHtml: string.Empty,
        TestConsultantCommentsHtml: string.Empty,
        TestConsultantCommentsTitle: "Comments :",
        ShowUkasLogo: false,
        Signatory: null,
        ParticipantOptions: [],
        SelectedParticipantId: Guid.Empty,
        ViewerOptions: [],
        SelectedViewerId: Guid.Empty,
        Tables: [],
        Ratings: null,
        Attachment: null,
        Print: new PTL.Contracts.Distribution.TabulationPrintResponse([], [], []),
        DistributionHeading: "D1a/S1 BT: Brucella");
}

internal sealed class FakeRazorViewRenderer(string html) : IRazorViewRenderer
{
    public Task<string> RenderAsync(ActionContext actionContext, string viewPath, object model) => Task.FromResult(html);
}

internal sealed class FakeWebHostEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "PTL.ExternalWeb.Tests";

    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

    public string WebRootPath { get; set; } = string.Empty;

    public string EnvironmentName { get; set; } = "Development";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
