using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using PTL.Auth.Cidm.Options;
using PTL.Common.Auth;

namespace PTL.Auth.Cidm.TokenRefresh;

/// <summary>
/// Exchanges a CIDM refresh_token for a new token set ahead of access token expiry, per the "Refresh
/// expired token(s)" section of the onboarding guide.
/// </summary>
public sealed class CidmTokenRefreshService : OidcTokenRefreshService, ICidmTokenRefreshService
{
    public const string HttpClientName = "CidmTokenRefresh";

    public CidmTokenRefreshService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
        IOptions<CidmOptions> cidmOptions)
        : base(
            httpClientFactory,
            oidcOptions,
            CidmAuthenticationDefaults.AuthenticationScheme,
            HttpClientName,
            cidmOptions.Value.ClientId,
            cidmOptions.Value.ClientSecret,
            cidmOptions.Value.AllScopes)
    {
    }
}
