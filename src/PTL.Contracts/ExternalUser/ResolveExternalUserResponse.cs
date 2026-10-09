namespace PTL.Contracts.ExternalUser;

/// <summary>Response body for <c>POST /api/external-users/resolve</c>.</summary>
public sealed record ResolveExternalUserResponse(
    string DisplayName,
    IReadOnlyList<string> Roles,
    Guid? ParticipantId,
    string? LabCode,
    Guid? ViewerId,
    Guid? TestConsultantId);
