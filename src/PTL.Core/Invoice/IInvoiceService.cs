namespace PTL.Core.Invoice;

public sealed record PendingInvoiceSummary(int EligibleContractCount, int OptOutContractCount, int NonFeePayingItemCount);

public sealed record InvoiceGenerationOutcome(bool Success, int ContractCount, string? CsvStorageKey, string? ErrorMessage);

public interface IInvoiceService
{
    Task<PendingInvoiceSummary> GetPendingSummaryAsync(CancellationToken cancellationToken = default);

    Task<InvoiceGenerationOutcome> GenerateInvoicesAsync(string generatedBy, CancellationToken cancellationToken = default);

    Task ResetInvoicedFlagsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceAuditEntity>> GetAuditHistoryAsync(CancellationToken cancellationToken = default);
}
