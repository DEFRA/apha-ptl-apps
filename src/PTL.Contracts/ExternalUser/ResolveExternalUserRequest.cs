namespace PTL.Contracts.ExternalUser;

/// <summary>Request body for <c>POST /api/external-users/resolve</c>.</summary>
public sealed record ResolveExternalUserRequest(
    Guid SsoIdExt,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);
