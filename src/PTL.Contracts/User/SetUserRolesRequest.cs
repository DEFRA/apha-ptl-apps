namespace PTL.Contracts.User;

// Posted to PUT /api/users/{userId}/roles - the complete desired set of role ids for that user
// (the service computes the add/remove diff against the existing tlnkUserRoles rows itself).
public sealed record SetUserRolesRequest(IReadOnlyList<Guid> RoleIds);
