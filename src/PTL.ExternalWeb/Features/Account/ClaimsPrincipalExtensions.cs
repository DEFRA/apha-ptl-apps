using System.Security.Claims;

namespace PTL.ExternalWeb.Features.Account;

/// <summary>
/// Single place that reads the roles <see cref="ExternalUserResolver"/> resolved at sign-in -
/// consumed by both <c>HomeController</c> and <c>_HeaderMenu.cshtml</c>, so role-gating logic
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

    /// <summary>The resolved Participant's own id, or null if the Participant role didn't resolve
    /// (e.g. a Viewer/Test Consultant-only user).</summary>
    public static Guid? GetParticipantId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(ExternalUserClaimTypes.ParticipantId)?.Value, out var id) ? id : null;

    /// <summary>The resolved Viewer's own id, or null if the Viewer role didn't resolve (a
    /// Participant-only user with no Viewer record).</summary>
    public static Guid? GetViewerId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(ExternalUserClaimTypes.ViewerId)?.Value, out var id) ? id : null;

    /// <summary>The resolved Test Consultant's own id, or null if the Test Consultant role didn't resolve.</summary>
    public static Guid? GetTestConsultantId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(ExternalUserClaimTypes.TestConsultantId)?.Value, out var id) ? id : null;
}
