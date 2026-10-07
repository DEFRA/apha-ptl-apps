using System.Security.Claims;

namespace PTL.Auth.Entra.Events;

/// <summary>
/// Optional hook for mapping a validated Entra ID identity onto the consuming application's own
/// user record. Resolved per-request from <c>HttpContext.RequestServices</c> (not constructor-injected
/// into <see cref="EntraOpenIdConnectEvents"/>, which is a singleton) - if the consuming app does
/// not register an implementation, <see cref="EntraOpenIdConnectEvents.TokenValidated"/> behaves
/// exactly as it did before this hook existed.
/// </summary>
public interface IEntraInternalUserResolver
{
    /// <summary>Resolves the application's own user record for the signed-in Entra ID identity.</summary>
    /// <param name="principal">The claims principal built from the validated id_token.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>Whether sign-in is permitted, and any claims to add when it is.</returns>
    Task<EntraInternalUserResolution> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}

/// <summary>Outcome of <see cref="IEntraInternalUserResolver.ResolveAsync"/>.</summary>
public sealed record EntraInternalUserResolution
{
    /// <summary>Gets a value indicating whether sign-in is permitted.</summary>
    public required bool IsAllowed { get; init; }

    /// <summary>Gets the local path to redirect to instead, when <see cref="IsAllowed"/> is <see langword="false"/>.</summary>
    public string? DenialRedirectPath { get; init; }

    /// <summary>Gets additional claims to add to the principal, when <see cref="IsAllowed"/> is <see langword="true"/>.</summary>
    public IReadOnlyDictionary<string, string>? Claims { get; init; }

    /// <summary>Creates an allowing resolution, optionally adding claims to the principal.</summary>
    /// <param name="claims">Additional claims to add, e.g. the application's own internal user id and roles.</param>
    public static EntraInternalUserResolution Allow(IReadOnlyDictionary<string, string>? claims = null) =>
        new() { IsAllowed = true, Claims = claims };

    /// <summary>Creates a denying resolution that redirects to <paramref name="redirectPath"/> instead of signing in.</summary>
    /// <param name="redirectPath">The local path to redirect to.</param>
    public static EntraInternalUserResolution Deny(string redirectPath) =>
        new() { IsAllowed = false, DenialRedirectPath = redirectPath };
}
