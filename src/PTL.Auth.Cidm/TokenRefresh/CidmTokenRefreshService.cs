using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using PTL.Auth.Cidm.Options;

namespace PTL.Auth.Cidm.TokenRefresh;

/// <summary>
/// Exchanges a CIDM refresh_token for a new token set ahead of access token expiry, per the "Refresh
/// expired token(s)" section of the onboarding guide. The token endpoint URL is read from the same
/// cached OIDC discovery document the sign-in handler itself uses, rather than being duplicated here.
/// </summary>
public sealed class CidmTokenRefreshService : ICidmTokenRefreshService
{
    public const string HttpClientName = "CidmTokenRefresh";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<OpenIdConnectOptions> _oidcOptions;
    private readonly CidmOptions _cidmOptions;

    public CidmTokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
        IOptions<CidmOptions> cidmOptions)
    {
        _httpClientFactory = httpClientFactory;
        _oidcOptions = oidcOptions;
        _cidmOptions = cidmOptions.Value;
    }

    public async Task<CidmTokenRefreshResult> RefreshAsync(string refreshToken, string redirectUri, CancellationToken cancellationToken = default)
    {
        var oidcOptions = _oidcOptions.Get(CidmAuthenticationDefaults.AuthenticationScheme);
        if (oidcOptions.ConfigurationManager is null)
        {
            return CidmTokenRefreshResult.Failed;
        }

        var configuration = await oidcOptions.ConfigurationManager.GetConfigurationAsync(cancellationToken);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var requestBody = new Dictionary<string, string>
        {
            ["client_id"] = _cidmOptions.ClientId,
            ["client_secret"] = _cidmOptions.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["scope"] = string.Join(' ', _cidmOptions.AllScopes),
            ["refresh_token"] = refreshToken,
            ["redirect_uri"] = redirectUri
        };

        using var response = await client.PostAsync(
            configuration.TokenEndpoint,
            new FormUrlEncodedContent(requestBody),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return CidmTokenRefreshResult.Failed;
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (payload?.AccessToken is null)
        {
            return CidmTokenRefreshResult.Failed;
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn);
        return new CidmTokenRefreshResult(true, payload.AccessToken, payload.IdToken, payload.RefreshToken, expiresAt);
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
