namespace PTL.Common.Graph;

/// <summary>
/// Configuration bound from the <c>Graph</c> section: the app registration used to call Microsoft Graph with the
/// client-credentials flow. Kept apart from the <c>Entra</c> sign-in section so it can move to its own app
/// registration (and secret) without a code change.
/// </summary>
public sealed class GraphOptions
{
    /// <summary>The configuration section name this type binds to ("Graph").</summary>
    public const string SectionName = "Graph";

    /// <summary>The Entra ID tenant identifier (GUID or verified domain).</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>The app registration's application (client) ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>The app registration's client secret. Never stored in source control or appsettings.json.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>The sign-in authority host the token is requested from.</summary>
    public string LoginAuthority { get; set; } = "https://login.microsoftonline.com";

    /// <summary>Base address of the Graph API, with a trailing slash so relative request paths resolve under it.</summary>
    public string BaseUrl { get; set; } = "https://graph.microsoft.com/v1.0/";

    /// <summary>"/.default" asks for every application permission granted to the app registration (e.g. User.Read.All).</summary>
    public string Scope { get; set; } = "https://graph.microsoft.com/.default";

    /// <summary>How long before a token expires a new one is requested.</summary>
    public TimeSpan RefreshBeforeExpiry { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>True once all three credentials are present.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>The OAuth2 v2.0 token endpoint for <see cref="TenantId"/>.</summary>
    public string TokenEndpoint => $"{LoginAuthority.TrimEnd('/')}/{Uri.EscapeDataString(TenantId)}/oauth2/v2.0/token";
}
