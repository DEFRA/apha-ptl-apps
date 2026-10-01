using PTL.Core.Invoice;

namespace PTL.Api.Tests.Invoice;

// In-memory IInvoiceRepository test double so InvoiceService can be tested without a real database
// or the spgaExportContractDetailsForAutomaticInvoicing/sppUpdateInvoiceItems/
// spiAuditInvoiceGeneration/sppResetInvoiceItems stored procedures.
internal sealed class FakeInvoiceRepository : IInvoiceRepository
{
    public InvoicePendingData PendingData { get; set; } = new([], []);
    public bool ThrowOnGetPendingData { get; set; }
    public bool MarkInvoicedCalled { get; private set; }
    public string? LastAuditWho { get; private set; }
    public bool ResetCalled { get; private set; }
    public List<InvoiceAuditEntity> AuditHistory { get; set; } = [];

    public Task<InvoicePendingData> GetPendingInvoiceDataAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnGetPendingData)
        {
            throw new InvalidOperationException("Simulated database failure.");
        }

        return Task.FromResult(PendingData);
    }

    public Task MarkInvoicedAndRecordAuditAsync(string auditWho, CancellationToken cancellationToken = default)
    {
        MarkInvoicedCalled = true;
        LastAuditWho = auditWho;
        return Task.CompletedTask;
    }

    public Task ResetInvoicedFlagsAsync(CancellationToken cancellationToken = default)
    {
        ResetCalled = true;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InvoiceAuditEntity>> GetAuditHistoryAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<InvoiceAuditEntity>>(AuditHistory);
}
