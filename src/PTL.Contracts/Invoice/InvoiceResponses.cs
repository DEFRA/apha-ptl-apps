namespace PTL.Contracts.Invoice;

// Legacy has no Invoice entity (docs/analysis/invoice-analysis.md) - these are read-models over the
// existing Contract/ParticipantScheme data, never a persisted "Invoice" aggregate.
public sealed record PendingInvoiceSummaryResponse(
    int FinancialYearId,
    string FinancialYear,
    int EligibleContractCount,
    int OptOutContractCount,
    int NonFeePayingItemCount,
    IReadOnlyList<string> NotificationRecipients);

public sealed record InvoiceGenerationResponse(bool Success, int ContractCount, string? CsvStorageKey, string? ErrorMessage);

public sealed record InvoiceAuditRecordResponse(Guid AuditInvoiceGenerationId, string AuditWho, DateTime AuditDate);
