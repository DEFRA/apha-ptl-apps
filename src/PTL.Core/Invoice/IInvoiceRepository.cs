namespace PTL.Core.Invoice;

public sealed record InvoicePendingData(IReadOnlyList<InvoiceContractEntity> Contracts, IReadOnlyList<InvoiceContractItemEntity> Items);

// Defined in Core (not Data) so IInvoiceService can depend on the abstraction without Core
// referencing Data; PTL.Data.Invoice.InvoiceRepository implements this.
public interface IInvoiceRepository
{
    // spgaExportContractDetailsForAutomaticInvoicing - one call, two result sets (contract header
    // rows then contract-item rows), exactly as legacy ContractCollection.DataPortal_Fetch reads it.
    Task<InvoicePendingData> GetPendingInvoiceDataAsync(CancellationToken cancellationToken = default);

    // sppUpdateInvoiceItems (mark invoiced) + spiAuditInvoiceGeneration (audit insert), executed in
    // one database transaction - mirrors legacy's single TransactionScope around both calls exactly.
    Task MarkInvoicedAndRecordAuditAsync(string auditWho, CancellationToken cancellationToken = default);

    // sppResetInvoiceItems - UAT-only bulk reset, no year scoping (matches legacy exactly).
    Task ResetInvoicedFlagsAsync(CancellationToken cancellationToken = default);

    // [NEEDS INVESTIGATION] no confirmed legacy read procedure exists for tblAuditInvoiceGeneration
    // (legacy never built a screen to view it) - this reads the table directly via parameterised
    // SQL rather than inventing an unconfirmed stored procedure name.
    Task<IReadOnlyList<InvoiceAuditEntity>> GetAuditHistoryAsync(CancellationToken cancellationToken = default);
}
