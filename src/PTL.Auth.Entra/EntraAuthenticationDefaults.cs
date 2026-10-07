namespace PTL.Auth.Entra;

/// <summary>Well-known constants for the Entra ID authentication scheme registered by <see cref="EntraAuthenticationExtensions"/>.</summary>
public static class EntraAuthenticationDefaults
{
    /// <summary>Scheme name used for the OpenID Connect handler registered by <see cref="EntraAuthenticationExtensions"/>.</summary>
    public const string AuthenticationScheme = "entra";
}
