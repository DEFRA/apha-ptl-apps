using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace PTL.Common.Graph;

/// <summary>
/// Requests a client-credentials token and caches it in memory until shortly before it expires, so a lookup does not
/// pay for a token round trip. A lock lets concurrent callers share one request. The token is never persisted or logged.
/// </summary>
public sealed class GraphTokenProvider(IHttpClientFactory httpClientFactory, IOptions<GraphOptions> options, TimeProvider timeProvider)
    : IGraphTokenProvider, IDisposable
{
    public const string HttpClientName = "GraphToken";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile CachedToken? _cached;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (ValidCached() is { } token)
        {
            return token;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have refreshed it while this one waited for the lock.
            if (ValidCached() is { } refreshed)
            {
                return refreshed;
            }

            var fresh = await RequestTokenAsync(cancellationToken);
            _cached = fresh;
            return fresh.Value;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private string? ValidCached() =>
        _cached is { } cached && timeProvider.GetUtcNow() < cached.RefreshAt ? cached.Value : null;

    private async Task<CachedToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            throw new GraphException("Microsoft Graph is not configured: Graph:TenantId, Graph:ClientId and Graph:ClientSecret are required.");
        }

        var form = new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret,
            ["grant_type"] = "client_credentials",
            ["scope"] = settings.Scope
        };

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsync(settings.TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GraphException($"Microsoft Graph token request failed with HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (string.IsNullOrEmpty(payload?.AccessToken))
        {
            throw new GraphException("Microsoft Graph token response did not contain an access token.");
        }

        var lifetime = TimeSpan.FromSeconds(payload.ExpiresIn) - settings.RefreshBeforeExpiry;
        return new CachedToken(payload.AccessToken, timeProvider.GetUtcNow() + (lifetime > TimeSpan.Zero ? lifetime : TimeSpan.Zero));
    }

    private sealed record CachedToken(string Value, DateTimeOffset RefreshAt);

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
