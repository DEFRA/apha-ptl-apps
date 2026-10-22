using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace PTL.Common.Auth;

/// <summary>
/// Exchanges a refresh_token for a new token set ahead of access token expiry. The token endpoint URL is
/// read from the same cached OIDC discovery document the sign-in handler itself uses, rather than being
/// duplicated here. Each identity provider supplies only its scheme, HTTP client name and client settings.
/// </summary>
public abstract class OidcTokenRefreshService : ITokenRefreshService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<OpenIdConnectOptions> _oidcOptions;
    private readonly string _authenticationScheme;
    private readonly string _httpClientName;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _scope;

    protected OidcTokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
        string authenticationScheme,
        string httpClientName,
        string clientId,
        string clientSecret,
        IEnumerable<string> scopes)
    {
        _httpClientFactory = httpClientFactory;
        _oidcOptions = oidcOptions;
        _authenticationScheme = authenticationScheme;
        _httpClientName = httpClientName;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _scope = string.Join(' ', scopes);
    }

    public async Task<TokenRefreshResult> RefreshAsync(string refreshToken, string redirectUri, CancellationToken cancellationToken = default)
    {
        var oidcOptions = _oidcOptions.Get(_authenticationScheme);
        if (oidcOptions.ConfigurationManager is null)
        {
            return TokenRefreshResult.Failed;
        }

        var configuration = await oidcOptions.ConfigurationManager.GetConfigurationAsync(cancellationToken);

        var client = _httpClientFactory.CreateClient(_httpClientName);
        var requestBody = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["grant_type"] = "refresh_token",
            ["scope"] = _scope,
            ["refresh_token"] = refreshToken,
            ["redirect_uri"] = redirectUri
        };

        using var response = await client.PostAsync(
            configuration.TokenEndpoint,
            new FormUrlEncodedContent(requestBody),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return TokenRefreshResult.Failed;
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (payload?.AccessToken is null)
        {
            return TokenRefreshResult.Failed;
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn);
        return new TokenRefreshResult(true, payload.AccessToken, payload.IdToken, payload.RefreshToken, expiresAt);
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
