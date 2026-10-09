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
    string? LabCode,
    Guid? ViewerId,
    Guid? TestConsultantId,
    // Only ever true for a resolved Participant whose linked Customer has CanOrderOnline set -
    // see ExternalUserService.ResolveParticipantAsync.
    bool CanOrderOnline = false)
{
    public bool IsResolved => Roles.Count > 0;
}
