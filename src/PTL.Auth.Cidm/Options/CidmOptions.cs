namespace PTL.Auth.Cidm.Options;

public sealed class CidmOptions
{
    public const string SectionName = "Cidm";

    /// <summary>Per-environment DEFRA IdP Hub base address, e.g. https://your-account.cpdev.cui.defra.gov.uk/idphub/b2c.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Per-environment B2C policy name, e.g. b2c_1a_cui_cpdev_signupsignin.</summary>
    public string Policy { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>DEFRA-specific parameter required on the /authorize request; not part of the OIDC spec.</summary>
    public string ServiceId { get; set; } = string.Empty;

    public string ResponseType { get; set; } = "code";

    public string ResponseMode { get; set; } = "form_post";

    public IReadOnlyList<string> Scopes { get; set; } = ["openid", "offline_access"];

    public string CallbackPath { get; set; } = "/signin-oidc";

    public string SignedOutCallbackPath { get; set; } = "/signout-oidc";

    /// <summary>How long before access token expiry a background refresh is attempted.</summary>
    public TimeSpan RefreshBeforeExpiry { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Composed rather than stored directly, so environments only need to supply <see cref="Address"/> and
    /// <see cref="Policy"/> - see the CIDM onboarding guide's "OpenID Connect configuration" section.
    /// </summary>
    public string MetadataAddress => $"{Address.TrimEnd('/')}/{Policy}/.well-known/openid-configuration";

    /// <summary>
    /// The client_id must also be sent as a scope entry (per the CIDM guide) to request an access token, in
    /// addition to the standard scopes.
    /// </summary>
    public IEnumerable<string> AllScopes => Scopes.Append(ClientId);
}
