namespace PTL.Contracts.ExternalUser;

/// <summary>Response body for <c>POST /api/external-users/resolve</c>.</summary>
/// <param name="Status">Whether the user was matched and may sign in; every other value is a denial reason.</param>
/// <param name="DisplayName">The name to show for the user.</param>
/// <param name="Roles">The roles to grant (see <see cref="ExternalRoleNames"/>); empty unless <paramref name="Status"/> is Resolved.</param>
/// <param name="Labs">The active labs a Participant or Lab User can act for.</param>
/// <param name="UserType">The kind of record matched, or <see langword="null"/> when not matched.</param>
/// <param name="ParticipantId">The lab to act for - only set when the user has exactly one active lab.</param>
/// <param name="LabCode">The code of <paramref name="ParticipantId"/>'s lab.</param>
/// <param name="ViewerId">The matched Viewer record.</param>
/// <param name="TestConsultantId">The matched Test Consultant record.</param>
/// <param name="LabUserId">The matched Lab User record.</param>
/// <param name="CanOrderOnline">Whether the single active lab may order online.</param>
/// <param name="CanEditDetails">Whether the single active lab is its customer's only active participant, so may edit the customer and participant details.</param>
public sealed record ResolveExternalUserResponse(
    ExternalUserResolutionStatus Status,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<ExternalLab> Labs,
    ExternalUserType? UserType = null,
    Guid? ParticipantId = null,
    string? LabCode = null,
    Guid? ViewerId = null,
    Guid? TestConsultantId = null,
    Guid? LabUserId = null,
    bool CanOrderOnline = false,
    bool CanEditDetails = false)
{
    /// <summary>Creates the response for a user who was not matched or may not sign in.</summary>
    public static ResolveExternalUserResponse Denied(ExternalUserResolutionStatus status, string displayName) =>
        new(status, displayName, [], []);
}
