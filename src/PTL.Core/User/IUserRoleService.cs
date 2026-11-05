namespace PTL.Core.User;

public interface IUserRoleService
{
    Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default);

    // Calls IUserRoleRepository.GetForUserAsync once per user - N+1, but matches legacy's own
    // GridView_Users_DataBound, which fetches UserRoleCollection.FetchUserRoleList per row too.
    Task<IReadOnlyList<UserRoleRow>> GetUserRoleGridAsync(CancellationToken cancellationToken = default);

    // Sets user's roles to exactly roleIds (computes the add/remove diff itself). Blocks (Success
    // = false, Message populated) if this would remove the Admin role from actingUserId themself.
    Task<SetUserRolesResult> SetUserRolesAsync(Guid userId, IReadOnlyList<Guid> roleIds, Guid? actingUserId, CancellationToken cancellationToken = default);

    // Saves every row's desired role set in ONE pass: fetches users/roles once (rather than once
    // per row) and applies every user's add/remove diff plus audit entry in a single DB
    // transaction. Used by the Manage User Roles "save the whole grid" action instead of calling
    // SetUserRolesAsync once per row, since that per-row loop (each re-querying all users/roles
    // and opening its own transaction) caused intermittent gateway timeouts on larger grids.
    Task<IReadOnlyList<SetUserRolesItemResult>> SetUserRolesBatchAsync(
        IReadOnlyList<(Guid UserId, IReadOnlyList<Guid> RoleIds)> rows,
        Guid? actingUserId,
        CancellationToken cancellationToken = default);

    // Revokes every role assignment then deletes the user record. Blocks (Success = false,
    // Message populated) if the target is actingUserId - matches the self-Admin-removal guard's
    // reasoning: an administrator must never be able to lock themselves out.
    Task<UserRemoveResult> RemoveUserAsync(Guid userId, Guid? actingUserId, CancellationToken cancellationToken = default);
}
