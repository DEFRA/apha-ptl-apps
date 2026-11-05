using System.Diagnostics.CodeAnalysis;
using Microsoft.Playwright;

namespace PTL.ExternalWeb.Infrastructure.Pdf;

public sealed record PdfRenderOptions(
    bool PreferCssPageSize = true,
    bool PrintBackground = true,
    string Format = "A4",
    bool Landscape = false,
    float Scale = 1f,
    string MarginTop = "12mm",
    string MarginBottom = "12mm",
    string MarginLeft = "10mm",
    string MarginRight = "10mm",
    string? HeaderTemplate = null,
    string? FooterTemplate = null)
{
    public static PdfRenderOptions Default { get; } = new();

    public bool DisplayHeaderFooter => HeaderTemplate is not null || FooterTemplate is not null;
}

public interface IHtmlToPdfRenderer
{
    Task<byte[]> RenderAsync(string html, PdfRenderOptions? options = null, CancellationToken cancellationToken = default);
}

// Seam over the Playwright driver so the render/retry logic can be tested without a real browser.
internal interface IPdfBrowserLauncher : IAsyncDisposable
{
    Task<IBrowser> LaunchAsync(string? channel, IReadOnlyList<string> args);
}

[ExcludeFromCodeCoverage(Justification = "Thin wrapper over the Playwright driver; needs a real Chromium install.")]
internal sealed class PlaywrightBrowserLauncher : IPdfBrowserLauncher
{
    private IPlaywright? playwright;

    public async Task<IBrowser> LaunchAsync(string? channel, IReadOnlyList<string> args)
    {
        playwright ??= await Playwright.CreateAsync();
        return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = channel,
            Args = args
        });
    }

    public ValueTask DisposeAsync()
    {
        playwright?.Dispose();
        return ValueTask.CompletedTask;
    }
}

public sealed partial class PlaywrightHtmlToPdfRenderer : IHtmlToPdfRenderer, IAsyncDisposable
{
    private static readonly byte[] PdfSignature = "%PDF"u8.ToArray();

    private readonly ILogger<PlaywrightHtmlToPdfRenderer> logger;
    private readonly IPdfBrowserLauncher launcher;
    private readonly SemaphoreSlim renderSlots;
    private readonly SemaphoreSlim browserLock = new(1, 1);
    private readonly string? browserChannel;
    private readonly string[] browserArgs;

    private IBrowser? browser;

    public PlaywrightHtmlToPdfRenderer(IConfiguration configuration, ILogger<PlaywrightHtmlToPdfRenderer> logger)
        : this(configuration, logger, new PlaywrightBrowserLauncher())
    {
    }

    internal PlaywrightHtmlToPdfRenderer(IConfiguration configuration, ILogger<PlaywrightHtmlToPdfRenderer> logger, IPdfBrowserLauncher launcher)
    {
        this.logger = logger;
        this.launcher = launcher;
        renderSlots = new SemaphoreSlim(Math.Max(1, configuration.GetValue("PdfGeneration:MaxConcurrentRenders", 2)));
        browserChannel = configuration["PdfGeneration:BrowserChannel"];
        browserArgs = [.. (configuration.GetSection("PdfGeneration:ChromiumArgs").Get<string[]>() ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim())];
    }

    public async Task<byte[]> RenderAsync(string html, PdfRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        var renderOptions = options ?? PdfRenderOptions.Default;

        await renderSlots.WaitAsync(cancellationToken);
        try
        {
            var chromium = await GetBrowserAsync(cancellationToken);
            try
            {
                return await PrintAsync(chromium, html, renderOptions);
            }
            catch (PlaywrightException exception)
            {
                LogBrowserClosedDuringRender(exception);
                await DiscardBrowserAsync(chromium, cancellationToken);
                return await PrintAsync(await GetBrowserAsync(cancellationToken), html, renderOptions);
            }
        }
        finally
        {
            renderSlots.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        var closing = browser;
        if (closing is not null)
        {
            await CloseQuietlyAsync(closing);
        }

        await launcher.DisposeAsync();
        renderSlots.Dispose();
        browserLock.Dispose();
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (browser is { IsConnected: true } running)
        {
            return running;
        }

        await browserLock.WaitAsync(cancellationToken);
        try
        {
            if (browser is { IsConnected: true } started)
            {
                return started;
            }

            var existing = browser;
            if (existing is not null)
            {
                await CloseQuietlyAsync(existing);
            }

            browser = await launcher.LaunchAsync(string.IsNullOrWhiteSpace(browserChannel) ? null : browserChannel.Trim(), browserArgs);
            return browser;
        }
        finally
        {
            browserLock.Release();
        }
    }

    private async Task DiscardBrowserAsync(IBrowser failed, CancellationToken cancellationToken)
    {
        await browserLock.WaitAsync(cancellationToken);
        try
        {
            if (ReferenceEquals(browser, failed))
            {
                browser = null;
                await CloseQuietlyAsync(failed);
            }
        }
        finally
        {
            browserLock.Release();
        }
    }

    private static async Task<byte[]> PrintAsync(IBrowser chromium, string html, PdfRenderOptions options)
    {
        await using var context = await chromium.NewContextAsync(new BrowserNewContextOptions { JavaScriptEnabled = false });
        var page = await context.NewPageAsync();
        await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load });

        var pdf = await page.PdfAsync(new PagePdfOptions
        {
            Format = options.Format,
            Landscape = options.Landscape,
            Scale = options.Scale,
            PrintBackground = options.PrintBackground,
            PreferCSSPageSize = options.PreferCssPageSize,
            DisplayHeaderFooter = options.DisplayHeaderFooter,
            HeaderTemplate = options.HeaderTemplate ?? "<span></span>",
            FooterTemplate = options.FooterTemplate ?? "<span></span>",
            Margin = new Margin
            {
                Top = options.MarginTop,
                Bottom = options.MarginBottom,
                Left = options.MarginLeft,
                Right = options.MarginRight
            }
        });

        if (pdf.Length < PdfSignature.Length || !pdf.AsSpan(0, PdfSignature.Length).SequenceEqual(PdfSignature))
        {
            throw new InvalidOperationException("The browser returned data that is not a PDF.");
        }

        return pdf;
    }

    private async Task CloseQuietlyAsync(IBrowser closing)
    {
        try
        {
            await closing.DisposeAsync();
        }
        catch (PlaywrightException exception)
        {
            LogBrowserCloseFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The PDF browser closed during render; retrying once with a new browser.")]
    private partial void LogBrowserClosedDuringRender(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The PDF browser could not be closed cleanly.")]
    private partial void LogBrowserCloseFailed(Exception exception);
}
