using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using PTL.ApiClient;
using PTL.InternalWeb.Notifications;

namespace PTL.InternalWeb.Features.Invoice;

// Invoice Generation is its own domain (docs/migration/invoice-migration.md "Controller Ownership") -
// a batch-processing workflow with its own audit/CSV/notification concerns, deliberately not merged
// into ContractController. Authentication/authorization are out of scope for this phase, consistent
// with every other InternalWeb controller.
public class InvoiceController(
    IInvoiceApiClient invoiceApiClient,
    IHostEnvironment hostEnvironment,
    ILogger<InvoiceController> logger) : Controller
{
    private static readonly Action<ILogger, int, Exception?> LogGeneratedMessage =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, nameof(LogGeneratedMessage)),
            "Invoices were generated for {ContractCount} contracts");

    private static readonly Action<ILogger, Exception?> LogGenerationFailedMessage =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2, nameof(LogGenerationFailedMessage)),
            "Invoice generation failed");

    private static readonly Action<ILogger, Guid, Exception?> LogDownloadNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(3, nameof(LogDownloadNotFoundMessage)),
            "No generated invoice CSV found for generation {GenerationId}");

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var pending = await invoiceApiClient.GetPendingAsync(cancellationToken);
        return View(InvoiceGenerationViewModel.From(pending, CanReset()));
    }

    // Legacy BtnGenerateInvoices_Click - one confirm dialog, then generate/mark-invoiced/audit/
    // notify in a single server-side action (docs/analysis/invoice-analysis.md).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(CancellationToken cancellationToken)
    {
        var pending = await invoiceApiClient.GetPendingAsync(cancellationToken);
        if (pending is not { EligibleContractCount: > 0 })
        {
            TempData.SetNotification(NotificationType.Error, "There are no outstanding invoices to generate.");
            return RedirectToAction(nameof(Index));
        }

        var result = await invoiceApiClient.GenerateAsync(cancellationToken);

        if (result is { Success: true })
        {
            LogGeneratedMessage(logger, result.ContractCount, null);
            TempData.SetNotification(NotificationType.Success, "Invoices were successfully generated.");
        }
        else
        {
            LogGenerationFailedMessage(logger, null);
            TempData.SetNotification(NotificationType.Error, result?.ErrorMessage ?? "Invoices could not be generated. Please try again later.");
        }

        return RedirectToAction(nameof(Index));
    }

    // Legacy BtnResetInvoices_Click - UAT only. Legacy gated this by an app-setting that could be
    // left "true" in Production by configuration drift; this gates it structurally by environment
    // instead (docs/migration/invoice-migration.md "Risks").
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(CancellationToken cancellationToken)
    {
        if (!CanReset())
        {
            return NotFound();
        }

        await invoiceApiClient.ResetAsync(cancellationToken);
        TempData.SetNotification(NotificationType.Success, "Invoices were reset.");
        return RedirectToAction(nameof(Index));
    }

    // Target of the downloadUrl emailed by GOV.UK Notify. The CSV is streamed from S3 via the API,
    // so the bucket is never exposed and every download goes through PTLIMS.
    [HttpGet]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var download = await invoiceApiClient.DownloadCsvAsync(id, cancellationToken);
        if (download is null)
        {
            LogDownloadNotFoundMessage(logger, id, null);
            return NotFound();
        }

        return File(download.Content, "text/csv", download.FileName);
    }

    private bool CanReset() => !hostEnvironment.IsProduction();
}
