using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using PTL.Auth.Entra.Options;

namespace PTL.Auth.Entra.TokenRefresh;

/// <summary>
/// Exchanges an Entra ID refresh_token for a new token set ahead of access token expiry. The token
/// endpoint URL is read from the same cached OIDC discovery document the sign-in handler itself
/// uses, rather than being duplicated here.
/// </summary>
public sealed class EntraTokenRefreshService : IEntraTokenRefreshService
{
    public const string HttpClientName = "EntraTokenRefresh";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<OpenIdConnectOptions> _oidcOptions;
    private readonly EntraOptions _entraOptions;

    public EntraTokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
        IOptions<EntraOptions> entraOptions)
    {
        _httpClientFactory = httpClientFactory;
        _oidcOptions = oidcOptions;
        _entraOptions = entraOptions.Value;
    }

    public async Task<EntraTokenRefreshResult> RefreshAsync(string refreshToken, string redirectUri, CancellationToken cancellationToken = default)
    {
        var oidcOptions = _oidcOptions.Get(EntraAuthenticationDefaults.AuthenticationScheme);
        if (oidcOptions.ConfigurationManager is null)
        {
            return EntraTokenRefreshResult.Failed;
        }

        var configuration = await oidcOptions.ConfigurationManager.GetConfigurationAsync(cancellationToken);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var requestBody = new Dictionary<string, string>
        {
            ["client_id"] = _entraOptions.ClientId,
            ["client_secret"] = _entraOptions.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["scope"] = string.Join(' ', _entraOptions.Scopes),
            ["refresh_token"] = refreshToken,
            ["redirect_uri"] = redirectUri
        };

        using var response = await client.PostAsync(
            configuration.TokenEndpoint,
            new FormUrlEncodedContent(requestBody),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return EntraTokenRefreshResult.Failed;
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (payload?.AccessToken is null)
        {
            return EntraTokenRefreshResult.Failed;
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn);
        return new EntraTokenRefreshResult(true, payload.AccessToken, payload.IdToken, payload.RefreshToken, expiresAt);
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
