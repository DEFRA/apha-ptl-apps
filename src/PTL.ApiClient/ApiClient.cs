using System.Net.Http.Json;

namespace PTL.ApiClient;

public interface IApiClient
{
    Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api, shared by every web front-end (PTL.InternalWeb,
// PTL.ExternalWeb, ...) so each one gets its own copy of this assembly at build/publish time
// rather than depending on a separately deployed library.
public sealed class ApiClient(HttpClient httpClient) : IApiClient
{
    public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<ApiHealthResponse>("/health", cancellationToken);
}

public sealed record ApiHealthResponse(string? Status, double UptimeSeconds, DateTime TimestampUtc);
