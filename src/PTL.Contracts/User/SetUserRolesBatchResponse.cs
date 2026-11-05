namespace PTL.Contracts.User;

// Result for a single user row within a SetUserRolesBatchResponse. Success = false (with Message
// populated) when that row's change is blocked by a business rule (e.g. removing your own Admin
// role) - a legitimate outcome, not a malformed request.
public sealed record SetUserRolesItemResult(Guid UserId, bool Success, string? Message);

// Response to PUT /api/users/roles - one result per submitted row.
public sealed record SetUserRolesBatchResponse(IReadOnlyList<SetUserRolesItemResult> Results);
