namespace PTL.Auth.Entra.Options;

/// <summary>Configuration bound from the <c>Entra</c> section, controlling Microsoft Entra ID OpenID Connect sign-in.</summary>
public sealed class EntraOptions
{
    /// <summary>The configuration section name this type binds to ("Entra").</summary>
    public const string SectionName = "Entra";

    /// <summary>
    /// The Entra ID (Azure AD) tenant identifier - either the tenant's GUID or a verified domain name
    /// (e.g. "defra.onmicrosoft.com"). Required; see <see cref="Authority"/>.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>The app registration's application (client) ID. Required.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>The app registration's client secret. Never stored in source control or appsettings.json.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>OAuth2 <c>response_type</c> requested on the /authorize call.</summary>
    public string ResponseType { get; set; } = "code";

    /// <summary>
    /// OpenID Connect <c>response_mode</c>. "form_post" requires <c>SameSite=None</c>+<c>Secure</c>
    /// cookies (HTTPS-only); <see cref="EntraAuthenticationExtensions"/> falls back to <c>query</c>
    /// automatically in the Development environment so local HTTP testing still works.
    /// </summary>
    public string ResponseMode { get; set; } = "form_post";

    /// <summary>
    /// OAuth2 scopes requested. "profile" is required to receive the name/given_name/family_name
    /// claims; "offline_access" is required to receive a refresh_token so the session can be
    /// refreshed ahead of access token expiry (see <see cref="RefreshBeforeExpiry"/>). The
    /// on-premises Active Directory claims this app matches users by (onprem_samaccountname,
    /// onprem_domainname) are delivered as optional ID token claims configured on the app
    /// registration itself (Azure Portal: App registration -&gt; Token configuration -&gt; Add
    /// optional claim -&gt; ID token), not via an extra scope here.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; set; } = ["openid", "profile", "offline_access"];

    /// <summary>Path the OIDC handler listens on for the identity provider's authorization code callback.</summary>
    public string CallbackPath { get; set; } = "/signin-entra";

    /// <summary>Path the OIDC handler listens on for the identity provider's post-logout redirect.</summary>
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-entra";

    /// <summary>
    /// Path registered as this app's front-channel logout URL in the Entra ID app registration, so a
    /// sign-out elsewhere (another app sharing this tenant, or an admin-forced sign-out) also clears
    /// this app's own session cookie.
    /// </summary>
    public string FrontChannelLogoutPath { get; set; } = "/signout-entra";

    /// <summary>How long before access token expiry a background refresh is attempted.</summary>
    public TimeSpan RefreshBeforeExpiry { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Composed rather than stored directly, so environments only need to supply <see cref="TenantId"/>.
    /// Uses the v2.0 (Microsoft identity platform) endpoint.
    /// </summary>
    public string Authority => $"https://login.microsoftonline.com/{TenantId}/v2.0";
}
