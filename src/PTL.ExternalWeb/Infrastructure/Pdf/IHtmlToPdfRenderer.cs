using Microsoft.Playwright;

namespace PTL.ExternalWeb.Infrastructure.Pdf;

public sealed record PdfRenderOptions(
    bool PreferCssPageSize = true,
    bool PrintBackground = true,
    string Format = "A4",
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

public sealed class PlaywrightHtmlToPdfRenderer(IConfiguration configuration, ILogger<PlaywrightHtmlToPdfRenderer> logger) : IHtmlToPdfRenderer, IAsyncDisposable
{
    private static readonly byte[] PdfSignature = "%PDF"u8.ToArray();

    private readonly SemaphoreSlim renderSlots = new(Math.Max(1, configuration.GetValue("PdfGeneration:MaxConcurrentRenders", 2)));
    private readonly SemaphoreSlim browserLock = new(1, 1);
    private readonly string? browserChannel = configuration["PdfGeneration:BrowserChannel"];

    private IPlaywright? playwright;
    private IBrowser? browser;

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
                logger.LogWarning(exception, "The PDF browser closed during render; retrying once with a new browser.");
                await DiscardBrowserAsync(chromium, cancellationToken);
                return await PrintAsync(await GetBrowserAsync(cancellationToken), html, renderOptions);
            }
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            logger.LogError(exception, "PDF rendering failed.");
            throw;
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

        playwright?.Dispose();
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

            playwright ??= await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Channel = string.IsNullOrWhiteSpace(browserChannel) ? null : browserChannel.Trim()
            });

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
            logger.LogWarning(exception, "The PDF browser could not be closed cleanly.");
        }
    }
}
