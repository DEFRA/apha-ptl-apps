using System.Net.Http.Json;
using PTL.Contracts.Invoice;

namespace PTL.ApiClient;

public interface IInvoiceApiClient
{
    Task<PendingInvoiceSummaryResponse?> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<InvoiceGenerationResponse?> GenerateAsync(CancellationToken cancellationToken = default);
    Task ResetAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceAuditRecordResponse>> GetAuditHistoryAsync(CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's /api/invoices endpoints, used only by
// PTL.InternalWeb (Invoice Generation is an internal-staff-only domain).
public sealed class InvoiceApiClient(HttpClient httpClient) : IInvoiceApiClient
{
    public Task<PendingInvoiceSummaryResponse?> GetPendingAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<PendingInvoiceSummaryResponse>("/api/invoices/pending", cancellationToken);

    public async Task<InvoiceGenerationResponse?> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync("/api/invoices/generate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InvoiceGenerationResponse>(cancellationToken);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync("/api/invoices/reset", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<InvoiceAuditRecordResponse>> GetAuditHistoryAsync(CancellationToken cancellationToken = default)
    {
        var history = await httpClient.GetFromJsonAsync<IReadOnlyList<InvoiceAuditRecordResponse>>("/api/invoices/audit-history", cancellationToken);
        return history ?? [];
    }
}
