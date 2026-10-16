using System.Security.Claims;

namespace PTL.ExternalWeb.Features.Account;

/// <summary>
/// Single place that reads the roles <see cref="ExternalUserResolver"/> resolved at sign-in -
/// consumed by both <c>HomeController</c> and <c>_SideNavigation.cshtml</c>, so role-gating logic
/// is not duplicated between the landing page and the navigation menu.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static IReadOnlyList<string> GetResolvedExternalRoles(this ClaimsPrincipal principal) =>
        principal.FindFirst(ExternalUserClaimTypes.ResolvedRoles)?.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? [];

    public static bool HasResolvedExternalRole(this ClaimsPrincipal principal, string roleName) =>
        principal.GetResolvedExternalRoles().Contains(roleName, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the resolved Participant's Customer is eligible for online ordering (see
    /// ExternalUserClaimTypes.CanOrderOnline) - gates the Orders section.</summary>
    public static bool CanOrderOnline(this ClaimsPrincipal principal) =>
        principal.HasClaim(claim => claim.Type == ExternalUserClaimTypes.CanOrderOnline);
}
