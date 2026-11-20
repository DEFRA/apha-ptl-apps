namespace PTL.Common.ApiAuthentication;

/// <summary>
/// Application-level authentication between the PTL web front-ends and PTL.Api. It identifies the
/// calling application only - never the end user, whose identity PTL.Api cannot verify.
/// </summary>
public sealed class ApiAuthenticationOptions
{
    public const string SectionName = "ApiAuthentication";

    public const string ApiKeyHeaderName = "X-Api-Key";

    public const string ClientAppHeaderName = "X-Client-App";

    public const string InternalWeb = "InternalWeb";

    public const string ExternalWeb = "ExternalWeb";

    public static IReadOnlyList<string> AllowedClientApps { get; } = [InternalWeb, ExternalWeb];

    /// <summary>
    /// Shared secret, supplied per environment from Parameter Store / Secrets Manager as
    /// <c>ApiAuthentication__ApiKey</c>. Never committed to appsettings.json.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The calling front-end's name (web front-ends only); one of <see cref="AllowedClientApps"/>.</summary>
    public string ClientApp { get; set; } = string.Empty;
}
