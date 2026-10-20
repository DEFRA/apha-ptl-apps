using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using PTL.Auth.Entra.Options;
using PTL.Common.Auth;

namespace PTL.Auth.Entra.TokenRefresh;

/// <summary>Exchanges an Entra ID refresh_token for a new token set ahead of access token expiry.</summary>
public sealed class EntraTokenRefreshService : OidcTokenRefreshService, IEntraTokenRefreshService
{
    public const string HttpClientName = "EntraTokenRefresh";

    public EntraTokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
        IOptions<EntraOptions> entraOptions)
        : base(
            httpClientFactory,
            oidcOptions,
            EntraAuthenticationDefaults.AuthenticationScheme,
            HttpClientName,
            entraOptions.Value.ClientId,
            entraOptions.Value.ClientSecret,
            entraOptions.Value.Scopes)
    {
    }
}
