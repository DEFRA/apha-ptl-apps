namespace PTL.Core.Invoice;

// tblAuditInvoiceGeneration row - 3 columns only (docs/analysis/invoice-analysis.md). Legacy only
// ever writes a row on a successful generation (the audit insert and the "mark invoiced" update
// share one transaction, and a failure before that transaction never reaches the insert) - so the
// mere existence of a row is itself the "outcome", there is no separate status column to preserve.
public class InvoiceAuditEntity
{
    public Guid AuditInvoiceGenerationId { get; set; }
    public string AuditWho { get; set; } = string.Empty;
    public DateTime AuditDate { get; set; }
}
