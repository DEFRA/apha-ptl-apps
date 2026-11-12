using PTL.Contracts.ExternalUser;

namespace PTL.Core.ExternalUser;

/// <summary>
/// Outcome of resolving a CIDM-authenticated external user to a single PTL user type. Only <see cref="ExternalUserResolutionStatus.Resolved"/>
/// carries roles, labs and ids; every other status is a denial with the reason.
/// </summary>
public sealed record ExternalUserResolutionResult(
    ExternalUserResolutionStatus Status,
    string DisplayName,
    ExternalUserType? UserType = null,
    IReadOnlyList<string>? Roles = null,
    IReadOnlyList<ExternalLab>? Labs = null,
    Guid? ParticipantId = null,
    string? LabCode = null,
    Guid? ViewerId = null,
    Guid? TestConsultantId = null,
    Guid? LabUserId = null,
    // Only ever true when exactly one active lab resolved and its Customer has CanOrderOnline set.
    bool CanOrderOnline = false,
    // Only ever true when exactly one active lab resolved and it is its customer's only active participant.
    bool CanEditDetails = false)
{
    public bool IsResolved => Status == ExternalUserResolutionStatus.Resolved;

    public static ExternalUserResolutionResult Denied(ExternalUserResolutionStatus status, string displayName) =>
        new(status, displayName);
}
