using System.Globalization;
using System.Security.Claims;
using PTL.Auth.Cidm.Claims;
using PTL.Contracts.ExternalUser;

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

    /// <summary>Whether the signed-in lab is its customer's only active participant, the legacy condition for editing
    /// the customer and participant details.</summary>
    public static bool CanEditDetails(this ClaimsPrincipal principal) =>
        principal.HasClaim(claim => claim.Type == ExternalUserClaimTypes.CanEditDetails);

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

    /// <summary>The CIDM contact id kept on the session - the key a lab is looked up by; null when absent or not a GUID.</summary>
    public static Guid? GetContactId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(CidmClaimTypes.ContactId)?.Value, out var id) ? id : null;

    /// <summary>
    /// Whether the signed-in user is a Lab User (as opposed to the Participant, who is the lab's primary contact). Read
    /// from the user type, not the roles, because a Lab User also holds the Participant role.
    /// </summary>
    public static bool IsLabUser(this ClaimsPrincipal principal) =>
        principal.HasClaim(ExternalUserClaimTypes.UserType, nameof(ExternalUserType.LabUser));

    /// <summary>How many active labs the user can work on, as counted at sign-in (0 for a Viewer or Test Consultant).</summary>
    public static int GetLabCount(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirst(ExternalUserClaimTypes.LabCount)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : 0;

    /// <summary>Whether the user has more than one lab, so may choose and switch between them.</summary>
    public static bool CanSwitchLab(this ClaimsPrincipal principal) => principal.GetLabCount() > 1;

    /// <summary>Whether the user has several labs and has not yet chosen the one to work on.</summary>
    public static bool MustSelectLab(this ClaimsPrincipal principal) => principal.CanSwitchLab() && principal.GetParticipantId() is null;
}
