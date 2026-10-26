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
    bool CanOrderOnline = false,
    // True when a Participant or Test Consultant record was found but excluded solely for being
    // inactive (Viewer has no inactive concept at all) - lets the caller distinguish "no matching
    // record exists" from "a matching record exists but is inactive" for sign-in denial messaging.
    bool HasInactiveMatch = false)
{
    public bool IsResolved => Roles.Count > 0;
}
