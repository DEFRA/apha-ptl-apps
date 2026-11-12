namespace PTL.ExternalWeb.Features.Account;

/// <summary>Claim types added to the principal once <see cref="ExternalUserResolver"/> resolves it.</summary>
public static class ExternalUserClaimTypes
{
    /// <summary>The CIDM display name to show on the landing page.</summary>
    public const string DisplayName = "displayName";

    /// <summary>
    /// The kind of PTL record the user was matched to - an <c>ExternalUserType</c> name (Participant, Viewer,
    /// TestConsultant or LabUser). Unlike <see cref="ResolvedRoles"/>, which also carries the roles implied by
    /// the type, this identifies the user precisely, e.g. to restrict a Lab User relative to a Participant.
    /// </summary>
    public const string UserType = "externalUserType";

    /// <summary>
    /// How many active labs a Participant or Lab User can act for (0 for other user types). When it is more
    /// than one, no lab is selected at sign-in and <see cref="ParticipantId"/> is absent until one is chosen.
    /// </summary>
    public const string LabCount = "labCount";

    /// <summary>The matched Lab User's own id, only present when the user is a Lab User.</summary>
    public const string LabUserId = "labUserId";

    /// <summary>
    /// Comma-separated list of the roles granted for the matched user type (see <c>ExternalUserService</c>). A
    /// single claim value, not one claim per role, because <c>CidmExternalUserResolution.Allow</c> only supports
    /// one value per claim type.
    /// </summary>
    public const string ResolvedRoles = "resolvedRoles";

    /// <summary>The lab code (legacy NavigationBar.ascx "Lab Number") of the lab the user acts for, only
    /// present when the user has exactly one active lab.</summary>
    public const string LabCode = "labCode";

    /// <summary>Present (value "true") only when the single active lab's Customer has
    /// CanOrderOnline set - gates the Orders section (NavigationProvider).</summary>
    public const string CanOrderOnline = "canOrderOnline";

    /// <summary>Present (value "true") only when the single active lab is its customer's only active
    /// participant - gates editing the customer and participant details (legacy CanEditCustomer/CanEditParticipant).</summary>
    public const string CanEditDetails = "canEditDetails";

    /// <summary>The own id of the lab the user acts for (a Participant, or a Lab User's single lab), only
    /// present when the user has exactly one active lab - lets participant-scoped features (e.g. Scheme List)
    /// query by id directly without re-resolving the user against PTL.Api.</summary>
    public const string ParticipantId = "participantId";

    /// <summary>The resolved Viewer's own id, only present when the user is a Viewer - lets viewer-scoped
    /// features (e.g. View Reports) query by id directly without re-resolving the user. Participants and Test
    /// Consultants are granted the Viewer role but have no Viewer record, so have no ViewerId.</summary>
    public const string ViewerId = "viewerId";

    /// <summary>The resolved Test Consultant's own id, only present when the user is a Test Consultant
    /// (own tblExternalTestConsultant row), so View Reports needs this in addition to ViewerId.</summary>
    public const string TestConsultantId = "testConsultantId";
}
