namespace PTL.Core.Invoice;

public sealed record PendingInvoiceSummary(int EligibleContractCount, int OptOutContractCount, int NonFeePayingItemCount);

public sealed record InvoiceGenerationOutcome(bool Success, int ContractCount, string? CsvStorageKey, string? ErrorMessage);

/// <summary>A generated CSV retrieved for download.</summary>
public sealed record InvoiceCsvDownload(string FileName, byte[] Content);

public interface IInvoiceService
{
    Task<PendingInvoiceSummary> GetPendingSummaryAsync(CancellationToken cancellationToken = default);

    Task<InvoiceGenerationOutcome> GenerateInvoicesAsync(string generatedBy, CancellationToken cancellationToken = default);

    /// <summary>Returns null when no CSV exists for the id. S3 is never exposed to the caller.</summary>
    Task<InvoiceCsvDownload?> GetGeneratedCsvAsync(Guid generationId, CancellationToken cancellationToken = default);

    Task ResetInvoicedFlagsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceAuditEntity>> GetAuditHistoryAsync(CancellationToken cancellationToken = default);
}
