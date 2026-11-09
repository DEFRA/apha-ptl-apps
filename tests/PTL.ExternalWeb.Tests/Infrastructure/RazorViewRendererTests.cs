using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Features.Tabulation;
using PTL.ExternalWeb.Infrastructure;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Infrastructure;

public class RazorViewRendererTests : IClassFixture<PtlExternalWebTestFactory>
{
    private const string PdfViewPath = TabulationPdfService.ReportPdfViewPath;

    private readonly PtlExternalWebTestFactory factory;

    public RazorViewRendererTests(PtlExternalWebTestFactory factory) => this.factory = factory;

    [Fact]
    public async Task RenderAsync_WithAnAppRelativeViewPath_RendersTheView()
    {
        using var scope = factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IRazorViewRenderer>();

        var html = await renderer.RenderAsync(ActionContextFor(scope, controller: null), PdfViewPath, Model());

        Assert.Contains("FINAL REPORT for D1a/S1 BT: Brucella", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_PdfView_PutsTheDistributionDateOnTheTitleRowAndTheHeaderImageAtTheTop()
    {
        using var scope = factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IRazorViewRenderer>();

        var html = await renderer.RenderAsync(
            ActionContextFor(scope, controller: null),
            PdfViewPath,
            Model() with { HeaderImageDataUri = "data:image/png;base64,AQID" });

        var header = html.IndexOf("class=\"ptl-pdf-header-image\"", StringComparison.Ordinal);
        var titleRow = html.IndexOf("class=\"ptl-pdf-title-row\"", StringComparison.Ordinal);
        var title = html.IndexOf("FINAL REPORT for D1a/S1 BT: Brucella", titleRow, StringComparison.Ordinal);
        var date = html.IndexOf("class=\"ptl-pdf-distribution-date\"", titleRow, StringComparison.Ordinal);

        Assert.True(header >= 0 && header < titleRow);
        Assert.True(title > titleRow && date > title);
        Assert.Contains("Date Of Distribution: 01/05/2026", html[date..], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_PdfView_WithoutAHeaderImage_OmitsIt()
    {
        using var scope = factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IRazorViewRenderer>();

        var html = await renderer.RenderAsync(ActionContextFor(scope, controller: null), PdfViewPath, Model());

        Assert.DoesNotContain("<img class=\"ptl-pdf-header-image\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_PdfView_PutsUkasCentredAboveTheVersionAndSignatureRow()
    {
        using var scope = factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IRazorViewRenderer>();
        var model = Model();
        var report = model.Page.Report with
        {
            ShowUkasLogo = true,
            Signatory = new TabulationSignatoryResponse("Version 1 - 01/05/2026", "A N Other", ["Weybridge", "Surrey"], "Issued: 01/05/2026")
        };

        var html = await renderer.RenderAsync(
            ActionContextFor(scope, controller: null),
            PdfViewPath,
            model with { Page = new TabulationReportPageViewModel(report), UkasLogoDataUri = "data:image/png;base64,AQID" });

        var footer = html.IndexOf("class=\"ptl-pdf-footer\"", StringComparison.Ordinal);
        var ukas = html.IndexOf("alt=\"UKAS accredited\"", footer, StringComparison.Ordinal);
        var row = html.IndexOf("class=\"ptl-pdf-footer-row\"", footer, StringComparison.Ordinal);
        var version = html.IndexOf("Version 1 - 01/05/2026", footer, StringComparison.Ordinal);
        var signature = html.IndexOf("A N Other", footer, StringComparison.Ordinal);

        Assert.True(footer >= 0);
        Assert.True(ukas > footer && row > ukas && version > row && signature > version);
        Assert.Contains("Issued: 01/05/2026", html[signature..], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_WithAViewNameAndControllerRoute_FindsTheViewThroughTheActionContext()
    {
        using var scope = factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IRazorViewRenderer>();

        var html = await renderer.RenderAsync(ActionContextFor(scope, controller: "Tabulation"), "ReportPdf", Model());

        Assert.Contains("FINAL REPORT for D1a/S1 BT: Brucella", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_WhenTheViewDoesNotExist_ListsTheSearchedLocations()
    {
        using var scope = factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IRazorViewRenderer>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => renderer.RenderAsync(ActionContextFor(scope, controller: "Tabulation"), "NoSuchView", Model()));

        Assert.Contains("Could not find view 'NoSuchView'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_WithABlankViewPath_Throws()
    {
        using var scope = factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IRazorViewRenderer>();

        await Assert.ThrowsAsync<ArgumentException>(
            () => renderer.RenderAsync(ActionContextFor(scope, controller: null), " ", Model()));
    }

    [Fact]
    public async Task RenderAsync_WithoutAnActionContext_Throws()
    {
        using var scope = factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IRazorViewRenderer>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => renderer.RenderAsync(null!, PdfViewPath, Model()));
    }

    private static ActionContext ActionContextFor(IServiceScope scope, string? controller)
    {
        var routeData = new RouteData();
        if (controller is not null)
        {
            routeData.Values["controller"] = controller;
        }

        return new ActionContext(new DefaultHttpContext { RequestServices = scope.ServiceProvider }, routeData, new ActionDescriptor());
    }

    private static TabulationPdfViewModel Model()
    {
        var id = Guid.NewGuid();
        var report = new TabulationReportResponse(
            id,
            IsPublished: true,
            DistributionTabulationId: id,
            Title: "FINAL REPORT for D1a/S1 BT: Brucella",
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
            Print: new TabulationPrintResponse([], [], []),
            DistributionHeading: "D1a/S1 BT: Brucella");

        return new TabulationPdfViewModel(new TabulationReportPageViewModel(report), UkasLogoDataUri: null);
    }
}
