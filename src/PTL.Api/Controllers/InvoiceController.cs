using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PTL.Contracts.Invoice;
using PTL.Core.Invoice;
using PTL.Core.Lookup;

namespace PTL.Api.Controllers;

// Invoice Generation is its own domain (docs/migration/invoice-migration.md "Controller Ownership") -
// a batch workflow with its own audit/CSV/notification concerns, deliberately not merged into
// ContractController.
[ApiController]
[Route("api/invoices")]
public sealed class InvoiceController(IInvoiceService invoiceService, ILookupService lookupService, IOptions<InvoiceNotificationOptions> notificationOptions) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<ActionResult<PendingInvoiceSummaryResponse>> GetPending(CancellationToken cancellationToken)
    {
        var summary = await invoiceService.GetPendingSummaryAsync(cancellationToken);
        var currentYears = await lookupService.GetCurrentYearsAsync(cancellationToken);
        var currentYear = currentYears.Count > 0 ? currentYears[0] : null;

        return Ok(new PendingInvoiceSummaryResponse(
            FinancialYearId: currentYear?.YearId ?? 0,
            FinancialYear: currentYear?.Year ?? string.Empty,
            EligibleContractCount: summary.EligibleContractCount,
            OptOutContractCount: summary.OptOutContractCount,
            NonFeePayingItemCount: summary.NonFeePayingItemCount,
            NotificationRecipients: notificationOptions.Value.Recipients));
    }

    [HttpPost("generate")]
    public async Task<ActionResult<InvoiceGenerationResponse>> Generate(CancellationToken cancellationToken)
    {
        var generatedBy = User?.Identity?.Name ?? "Guest";
        var outcome = await invoiceService.GenerateInvoicesAsync(generatedBy, cancellationToken);

        return Ok(new InvoiceGenerationResponse(outcome.Success, outcome.ContractCount, outcome.CsvStorageKey, outcome.ErrorMessage));
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken cancellationToken)
    {
        await invoiceService.ResetInvoicedFlagsAsync(cancellationToken);
        return NoContent();
    }

    // The bucket stays private - the CSV is streamed through the API, never linked to directly.
    [HttpGet("{generationId:guid}/csv")]
    public async Task<IActionResult> DownloadCsv(Guid generationId, CancellationToken cancellationToken)
    {
        var download = await invoiceService.GetGeneratedCsvAsync(generationId, cancellationToken);
        return download is null ? NotFound() : File(download.Content, "text/csv", download.FileName);
    }

    [HttpGet("audit-history")]
    public async Task<ActionResult<IReadOnlyList<InvoiceAuditRecordResponse>>> GetAuditHistory(CancellationToken cancellationToken)
    {
        var history = await invoiceService.GetAuditHistoryAsync(cancellationToken);
        return Ok(history.Select(a => new InvoiceAuditRecordResponse(a.AuditInvoiceGenerationId, a.AuditWho, a.AuditDate)).ToList());
    }
}
