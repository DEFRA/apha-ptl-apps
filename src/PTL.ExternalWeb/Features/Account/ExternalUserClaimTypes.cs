namespace PTL.ExternalWeb.Features.Account;

/// <summary>Claim types added to the principal once <see cref="ExternalUserResolver"/> resolves it.</summary>
public static class ExternalUserClaimTypes
{
    /// <summary>The CIDM display name to show on the landing page.</summary>
    public const string DisplayName = "displayName";

    /// <summary>
    /// Comma-separated list of the CIDM roles that resolved to an existing Participant, Viewer or
    /// Test Consultant record (roles CIDM reports but that don't resolve are omitted - see
    /// <c>ExternalUserResolver</c>). A single claim value, not one claim per role, because
    /// <c>CidmExternalUserResolution.Allow</c> only supports one value per claim type.
    /// </summary>
    public const string ResolvedRoles = "resolvedRoles";

    /// <summary>The resolved Participant's lab code (legacy NavigationBar.ascx "Lab Number"), only
    /// present when the Participant role resolved.</summary>
    public const string LabCode = "labCode";

    /// <summary>Present (value "true") only when the resolved Participant's Customer has
    /// CanOrderOnline set - gates the Orders section (NavigationProvider).</summary>
    public const string CanOrderOnline = "canOrderOnline";

    /// <summary>The resolved Participant's own id, only present when the Participant role
    /// resolved - lets participant-scoped features (e.g. Scheme List) query by id directly
    /// without re-resolving the user against PTL.Api.</summary>
    public const string ParticipantId = "participantId";

    /// <summary>The resolved Viewer's own id, only present when the Viewer role resolved (this
    /// also covers Test Consultants, who are always also resolved as a Viewer - see
    /// docs/Parity-Analysis/11-External-Role-Card-and-Menu-Visibility.md) - lets viewer-scoped
    /// features (e.g. View Reports) query by id directly without re-resolving the user.</summary>
    public const string ViewerId = "viewerId";

    /// <summary>The resolved Test Consultant's own id, only present when the Test Consultant role
    /// resolved - Test Consultants are resolved separately from Viewers (own tblExternalTestConsultant
    /// row, own SsoId), so View Reports needs this in addition to ViewerId.</summary>
    public const string TestConsultantId = "testConsultantId";
}
