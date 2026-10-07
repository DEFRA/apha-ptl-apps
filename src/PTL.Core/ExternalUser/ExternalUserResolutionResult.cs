namespace PTL.Core.ExternalUser;

/// <summary>
/// Outcome of resolving a CIDM-authenticated external user against the Participant, Viewer and
/// Test Consultant tables. A role is present in <see cref="Roles"/> only when a matching (or
/// newly linked) record was found - roles that did not resolve are omitted, never auto-created.
/// </summary>
public sealed record ExternalUserResolutionResult(
    string DisplayName,
    IReadOnlyList<string> Roles,
    Guid? ParticipantId,
    Guid? ViewerId,
    Guid? TestConsultantId)
{
    public bool IsResolved => Roles.Count > 0;
}
