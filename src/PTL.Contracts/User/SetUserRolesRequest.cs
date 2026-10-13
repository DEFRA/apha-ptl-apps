namespace PTL.Contracts.User;

// Posted to PUT /api/users/{userId}/roles - the complete desired set of role ids for that user
// (the service computes the add/remove diff against the existing tlnkUserRoles rows itself).
// ActingUserId is the signed-in admin making the change (from PTL.InternalWeb's resolved Entra
// claims) - used only for the "can't remove your own Admin role" guard, not persisted.
public sealed record SetUserRolesRequest(IReadOnlyList<Guid> RoleIds, Guid? ActingUserId = null);

