namespace PTL.Auth.Cidm;

public static class CidmAuthenticationDefaults
{
    /// <summary>Scheme name used for the OpenID Connect handler registered by <see cref="CidmAuthenticationExtensions"/>.</summary>
    public const string AuthenticationScheme = "oidc";

    /// <summary>
    /// <see cref="Microsoft.AspNetCore.Authentication.AuthenticationProperties"/> item key used to request
    /// CIDM's <c>forceReselection</c> parameter on the next challenge (organisation switching).
    /// </summary>
    public const string ForceReselectionProperty = "cidm:forceReselection";

    /// <summary>
    /// <see cref="Microsoft.AspNetCore.Authentication.AuthenticationProperties"/> item key used to pre-select a
    /// known relationship via CIDM's <c>relationshipId</c> parameter on the next challenge.
    /// </summary>
    public const string RelationshipIdProperty = "cidm:relationshipId";

    /// <summary>
    /// The OpenID Connect handler's remote (front-channel) sign-out endpoint. It must differ from the signed-out
    /// callback path (<c>/signout-oidc</c>, the post-logout redirect registered with CIDM): the handler's default for
    /// it is also <c>/signout-oidc</c>, and it is checked first, so the callback would only clear the cookie and
    /// return an empty page instead of redirecting to the signed-out page. CIDM is not registered to call this path.
    /// </summary>
    public const string RemoteSignOutPath = "/signout-remote-oidc";
}
