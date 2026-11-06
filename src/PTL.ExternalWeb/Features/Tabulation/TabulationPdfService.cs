using System.Net;
using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Infrastructure;
using PTL.ExternalWeb.Infrastructure.Pdf;

namespace PTL.ExternalWeb.Features.Tabulation;

public interface ITabulationPdfService
{
    Task<byte[]> GenerateAsync(ActionContext actionContext, TabulationReportPageViewModel model, CancellationToken cancellationToken = default);
}

public sealed record TabulationPdfViewModel(TabulationReportPageViewModel Page, string? UkasLogoDataUri);

public sealed class TabulationPdfService(
    IRazorViewRenderer viewRenderer,
    IHtmlToPdfRenderer pdfRenderer,
    IWebHostEnvironment hostEnvironment) : ITabulationPdfService
{
    public const string ReportPdfViewPath = "/Features/Tabulation/Views/ReportPdf.cshtml";

    public async Task<byte[]> GenerateAsync(ActionContext actionContext, TabulationReportPageViewModel model, CancellationToken cancellationToken = default)
    {
        var viewModel = new TabulationPdfViewModel(model, await BuildUkasLogoDataUriAsync(cancellationToken));
        var html = await viewRenderer.RenderAsync(actionContext, ReportPdfViewPath, viewModel);

        var footerTitle = WebUtility.HtmlEncode($"{model.Report.DistributionHeading} - {model.Report.TabulationName}.");
        var options = new PdfRenderOptions(
            Format: "A3",
            Landscape: true,
            Scale: 0.9f,
            MarginTop: "12mm",
            MarginBottom: "16mm",
            MarginLeft: "8mm",
            MarginRight: "8mm",
            FooterTemplate: "<div style=\"width:100%;font-size:8px;font-family:Arial,sans-serif;text-align:center\">"
                + $"{footerTitle} Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span></div>");

        return await pdfRenderer.RenderAsync(html, options, cancellationToken);
    }

    private async Task<string?> BuildUkasLogoDataUriAsync(CancellationToken cancellationToken)
    {
        var webRoot = hostEnvironment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            return null;
        }

        var ukasPath = Path.Combine(webRoot, "images", "ukas.png");
        if (!File.Exists(ukasPath))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(ukasPath, cancellationToken);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }
}
