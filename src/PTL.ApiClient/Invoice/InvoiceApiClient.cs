using System.Net.Http.Json;
using PTL.Contracts.Invoice;

namespace PTL.ApiClient;

public interface IInvoiceApiClient
{
    Task<PendingInvoiceSummaryResponse?> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<InvoiceGenerationResponse?> GenerateAsync(CancellationToken cancellationToken = default);
    Task ResetAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceAuditRecordResponse>> GetAuditHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns null when no CSV exists for the id.</summary>
    Task<InvoiceCsvDownloadResponse?> DownloadCsvAsync(Guid generationId, CancellationToken cancellationToken = default);
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

    public async Task<InvoiceCsvDownloadResponse?> DownloadCsvAsync(Guid generationId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/invoices/{generationId}/csv", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? $"PT_Invoices_{generationId}.csv";

        return new InvoiceCsvDownloadResponse(fileName, await response.Content.ReadAsByteArrayAsync(cancellationToken));
    }
}
