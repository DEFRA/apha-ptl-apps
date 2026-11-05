using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using PTL.ExternalWeb.Infrastructure.Pdf;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Infrastructure;

public class PlaywrightHtmlToPdfRendererTests
{
    private static PlaywrightHtmlToPdfRenderer CreateRenderer(FakePdfBrowserLauncher launcher, Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build();
        return new PlaywrightHtmlToPdfRenderer(configuration, NullLogger<PlaywrightHtmlToPdfRenderer>.Instance, launcher);
    }

    [Fact]
    public async Task RenderAsync_ReturnsThePdfAndPassesTheHtmlAndOptionsToTheBrowser()
    {
        var browser = new FakePdfBrowser();
        await using var renderer = CreateRenderer(new FakePdfBrowserLauncher(browser));
        var options = new PdfRenderOptions(
            Format: "A3", Landscape: true, Scale: 0.9f, MarginTop: "1mm", MarginBottom: "2mm", MarginLeft: "3mm", MarginRight: "4mm",
            FooterTemplate: "<div>footer</div>");

        var pdf = await renderer.RenderAsync("<html>hello</html>", options);

        Assert.Equal(FakePdfBrowser.ValidPdf, pdf);
        Assert.Equal("<html>hello</html>", browser.LastHtml);
        var pdfOptions = browser.LastPdfOptions!;
        Assert.Equal("A3", pdfOptions.Format);
        Assert.True(pdfOptions.Landscape);
        Assert.Equal(0.9f, pdfOptions.Scale);
        Assert.True(pdfOptions.DisplayHeaderFooter);
        Assert.Equal("<span></span>", pdfOptions.HeaderTemplate);
        Assert.Equal("<div>footer</div>", pdfOptions.FooterTemplate);
        Assert.Equal("1mm", pdfOptions.Margin!.Top);
        Assert.Equal("2mm", pdfOptions.Margin.Bottom);
        Assert.Equal("3mm", pdfOptions.Margin.Left);
        Assert.Equal("4mm", pdfOptions.Margin.Right);
    }

    [Fact]
    public async Task RenderAsync_WithoutOptions_UsesTheDefaults()
    {
        var browser = new FakePdfBrowser();
        await using var renderer = CreateRenderer(new FakePdfBrowserLauncher(browser));

        await renderer.RenderAsync("<html>hello</html>");

        var pdfOptions = browser.LastPdfOptions!;
        Assert.Equal("A4", pdfOptions.Format);
        Assert.False(pdfOptions.DisplayHeaderFooter);
        Assert.Equal("<span></span>", pdfOptions.FooterTemplate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenderAsync_WithBlankHtml_Throws(string html)
    {
        await using var renderer = CreateRenderer(new FakePdfBrowserLauncher());

        await Assert.ThrowsAsync<ArgumentException>(() => renderer.RenderAsync(html));
    }

    [Fact]
    public async Task RenderAsync_ReusesTheRunningBrowser()
    {
        var browser = new FakePdfBrowser();
        var launcher = new FakePdfBrowserLauncher(browser);
        await using var renderer = CreateRenderer(launcher);

        await renderer.RenderAsync("<p>one</p>");
        await renderer.RenderAsync("<p>two</p>");

        Assert.Single(launcher.Channels);
        Assert.Equal(2, browser.PrintCount);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData(" msedge ", "msedge")]
    public async Task RenderAsync_PassesTheTrimmedBrowserChannelToTheLauncher(string? configured, string? expected)
    {
        var launcher = new FakePdfBrowserLauncher(new FakePdfBrowser());
        var settings = new Dictionary<string, string?> { ["PdfGeneration:BrowserChannel"] = configured };
        await using var renderer = CreateRenderer(launcher, settings);

        await renderer.RenderAsync("<p>x</p>");

        Assert.Equal([expected], launcher.Channels);
    }

    [Fact]
    public async Task RenderAsync_WhenTheBrowserDisconnected_ClosesItAndLaunchesANewOne()
    {
        var first = new FakePdfBrowser();
        var second = new FakePdfBrowser();
        var launcher = new FakePdfBrowserLauncher(first, second);
        await using var renderer = CreateRenderer(launcher);
        await renderer.RenderAsync("<p>one</p>");
        first.Connected = false;

        await renderer.RenderAsync("<p>two</p>");

        Assert.Equal(2, launcher.Channels.Count);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.PrintCount);
    }

    [Fact]
    public async Task RenderAsync_WhenTheBrowserDiesMidRender_RetriesOnceWithANewBrowser()
    {
        var broken = new FakePdfBrowser { PdfFailure = new PlaywrightException("Target closed") };
        var healthy = new FakePdfBrowser();
        var launcher = new FakePdfBrowserLauncher(broken, healthy);
        await using var renderer = CreateRenderer(launcher);

        var pdf = await renderer.RenderAsync("<p>x</p>");

        Assert.Equal(FakePdfBrowser.ValidPdf, pdf);
        Assert.Equal(1, broken.DisposeCount);
        Assert.Equal(1, healthy.PrintCount);
    }

    [Fact]
    public async Task RenderAsync_WhenTheRetryAlsoFails_PropagatesTheError()
    {
        var first = new FakePdfBrowser { PdfFailure = new PlaywrightException("Target closed") };
        var second = new FakePdfBrowser { PdfFailure = new PlaywrightException("Still broken") };
        await using var renderer = CreateRenderer(new FakePdfBrowserLauncher(first, second));

        var error = await Assert.ThrowsAsync<PlaywrightException>(() => renderer.RenderAsync("<p>x</p>"));

        Assert.Contains("Still broken", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_WhenTheBrowserReturnsNonPdfData_Throws()
    {
        var browser = new FakePdfBrowser { PdfBytes = "not a pdf"u8.ToArray() };
        await using var renderer = CreateRenderer(new FakePdfBrowserLauncher(browser));

        await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.RenderAsync("<p>x</p>"));
    }

    [Fact]
    public async Task RenderAsync_WhenTheBrowserReturnsTooFewBytes_Throws()
    {
        var browser = new FakePdfBrowser { PdfBytes = "%P"u8.ToArray() };
        await using var renderer = CreateRenderer(new FakePdfBrowserLauncher(browser));

        await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.RenderAsync("<p>x</p>"));
    }

    [Fact]
    public async Task RenderAsync_WhenClosingTheFailedBrowserErrors_StillRetries()
    {
        var broken = new FakePdfBrowser
        {
            PdfFailure = new PlaywrightException("Target closed"),
            DisposeFailure = new PlaywrightException("Already gone")
        };
        var healthy = new FakePdfBrowser();
        await using var renderer = CreateRenderer(new FakePdfBrowserLauncher(broken, healthy));

        var pdf = await renderer.RenderAsync("<p>x</p>");

        Assert.Equal(FakePdfBrowser.ValidPdf, pdf);
    }

    [Fact]
    public async Task RenderAsync_WhenCancelledBeforeStarting_Throws()
    {
        await using var renderer = CreateRenderer(new FakePdfBrowserLauncher(new FakePdfBrowser()));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renderer.RenderAsync("<p>x</p>", cancellationToken: cancelled.Token));
    }

    [Fact]
    public async Task DisposeAsync_ClosesTheBrowserAndTheLauncher()
    {
        var browser = new FakePdfBrowser();
        var launcher = new FakePdfBrowserLauncher(browser);
        var renderer = CreateRenderer(launcher);
        await renderer.RenderAsync("<p>x</p>");

        await renderer.DisposeAsync();

        Assert.Equal(1, browser.DisposeCount);
        Assert.True(launcher.Disposed);
    }

    [Fact]
    public async Task DisposeAsync_WithoutAStartedBrowser_OnlyDisposesTheLauncher()
    {
        var launcher = new FakePdfBrowserLauncher();
        var renderer = CreateRenderer(launcher);

        await renderer.DisposeAsync();

        Assert.True(launcher.Disposed);
    }

    [Fact]
    public async Task RenderAsync_WithAHeaderTemplateOnly_TurnsOnHeaderAndFooter()
    {
        var browser = new FakePdfBrowser();
        await using var renderer = CreateRenderer(new FakePdfBrowserLauncher(browser));

        await renderer.RenderAsync("<p>x</p>", new PdfRenderOptions(HeaderTemplate: "<div>header</div>"));

        Assert.True(browser.LastPdfOptions!.DisplayHeaderFooter);
        Assert.Equal("<div>header</div>", browser.LastPdfOptions.HeaderTemplate);
    }

    [Fact]
    public async Task RenderAsync_WhenTwoRendersRaceToStartTheBrowser_LaunchesOnlyOnce()
    {
        var browser = new FakePdfBrowser();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launcher = new FakePdfBrowserLauncher(browser) { Gate = gate };
        await using var renderer = CreateRenderer(launcher);

        var first = renderer.RenderAsync("<p>one</p>");
        var second = renderer.RenderAsync("<p>two</p>");
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Single(launcher.Channels);
        Assert.Equal(2, browser.PrintCount);
    }

    [Fact]
    public async Task RenderAsync_HonoursTheConfiguredConcurrencyLimit()
    {
        var launcher = new FakePdfBrowserLauncher(new FakePdfBrowser());
        var settings = new Dictionary<string, string?> { ["PdfGeneration:MaxConcurrentRenders"] = "0" };
        await using var renderer = CreateRenderer(launcher, settings);

        var pdf = await renderer.RenderAsync("<p>x</p>");

        Assert.Equal(FakePdfBrowser.ValidPdf, pdf);
    }
}
