using PTL.ApiClient;
using PTL.Contracts.Invoice;

namespace PTL.InternalWeb.Tests.TestSupport;

// In-memory IInvoiceApiClient test double so InvoiceController can be tested without a real API call.
internal sealed class FakeInvoiceApiClient : IInvoiceApiClient
{
    public PendingInvoiceSummaryResponse? Pending { get; set; }
    public InvoiceGenerationResponse? GenerateResult { get; set; }
    public IReadOnlyList<InvoiceAuditRecordResponse> AuditHistory { get; set; } = [];
    public bool ResetCalled { get; private set; }
    public bool GenerateCalled { get; private set; }

    public Task<PendingInvoiceSummaryResponse?> GetPendingAsync(CancellationToken cancellationToken = default) => Task.FromResult(Pending);

    public Task<InvoiceGenerationResponse?> GenerateAsync(CancellationToken cancellationToken = default)
    {
        GenerateCalled = true;
        return Task.FromResult(GenerateResult);
    }

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        ResetCalled = true;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InvoiceAuditRecordResponse>> GetAuditHistoryAsync(CancellationToken cancellationToken = default) => Task.FromResult(AuditHistory);
}
