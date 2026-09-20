namespace PTL.InternalWeb.Authentication;

/// <summary>
/// Microsoft Entra ID app registration settings, bound from the "EntraId" configuration section.
/// </summary>
public sealed class EntraIdOptions
{
    public const string SectionName = "EntraId";

    /// <summary>The Microsoft identity platform instance. Defaults to the public cloud endpoint.</summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>The Entra ID (Azure AD) tenant ID (GUID or verified domain name).</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>The application (client) ID of the App Registration.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The confidential client secret for the App Registration. Must be supplied via an
    /// environment variable or AWS Secrets Manager in every real environment - never committed to
    /// appsettings.json.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>The v2.0 OIDC authority derived from <see cref="Instance"/> and <see cref="TenantId"/>.</summary>
    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}/v2.0";
}
