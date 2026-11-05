namespace PTL.Core.User;

public sealed record UserRoleRow(Guid UserId, string Username, string FriendlyName, IReadOnlyList<Guid> RoleIds);

public sealed record SetUserRolesResult(bool Success, string? Message);

public sealed record UserRemoveResult(bool Success, string? Message);

// Result for one row of a SetUserRolesBatchAsync call.
public sealed record SetUserRolesItemResult(Guid UserId, bool Success, string? Message);
