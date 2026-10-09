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

    // Revokes every role assignment then deletes the user record. Blocks (Success = false,
    // Message populated) if the target is actingUserId - matches the self-Admin-removal guard's
    // reasoning: an administrator must never be able to lock themselves out.
    Task<UserRemoveResult> RemoveUserAsync(Guid userId, Guid? actingUserId, CancellationToken cancellationToken = default);
}
