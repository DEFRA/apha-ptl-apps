namespace PTL.Contracts.User;

// Posted to PUT /api/users/roles - the complete desired role set for every user row shown on the
// Manage User Roles grid, saved in a single request/transaction instead of one PUT per user (which
// caused intermittent 502 gateway timeouts on grids with many users). ActingUserId is the signed-in
// admin making the change (from PTL.InternalWeb's resolved Entra claims) - used only for the "can't
// remove your own Admin role" guard, not persisted.
public sealed record SetUserRolesBatchRequest(IReadOnlyList<SetUserRolesBatchItem> Items, Guid? ActingUserId = null);

// One row's desired role set within a SetUserRolesBatchRequest.
public sealed record SetUserRolesBatchItem(Guid UserId, IReadOnlyList<Guid> RoleIds);
