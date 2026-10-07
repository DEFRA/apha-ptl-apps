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
}
