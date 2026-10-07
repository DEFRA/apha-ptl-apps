namespace PTL.Contracts.InternalUser;

/// <summary>
/// Response body for <c>POST /api/internal-users/resolve</c>. When <see cref="IsPermitted"/> is
/// <see langword="false"/> (no matching tblUsers row), every other field is default/empty - there
/// is no "limited access" allowance for internal users, unlike the external-user CIDM flow.
/// </summary>
public sealed record ResolveInternalUserResponse(
    bool IsPermitted,
    Guid? UserId,
    string FullName,
    string Department,
    IReadOnlyList<string> Roles);
