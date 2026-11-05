using PTL.ExternalWeb.Infrastructure.Pdf;

namespace PTL.ExternalWeb.Tests.TestSupport;

internal sealed class FakeHtmlToPdfRenderer(byte[]? pdf = null, Exception? throwOnRender = null) : IHtmlToPdfRenderer
{
    public List<string> RenderedHtml { get; } = [];

    public List<PdfRenderOptions?> RenderedOptions { get; } = [];

    public Task<byte[]> RenderAsync(string html, PdfRenderOptions? options = null, CancellationToken cancellationToken = default)
    {
        RenderedHtml.Add(html);
        RenderedOptions.Add(options);

        if (throwOnRender is not null)
        {
            throw throwOnRender;
        }

        return Task.FromResult(pdf ?? "%PDF-1.7"u8.ToArray());
    }
}
