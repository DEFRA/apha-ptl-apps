using PTL.Core.Audit;

namespace PTL.Core.User;

// The current user is resolved by PTL.InternalWeb (from the signed-in Entra claims) and passed in
// explicitly as actingUserId on every call that needs it, rather than resolved here via an
// injected "current user" service - see ICurrentUserProvider's removal: PTL.Api has no identity
// context of its own (no authentication is validated at this layer yet), so an ambient
// "who is calling" service here would either stay permanently stubbed or have to trust a
// self-reported value anyway. Threading it explicitly from the one layer that actually knows it
// keeps that trust boundary visible at every call site instead of hiding it behind a DI service.
public sealed class UserRoleService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUserRoleRepository userRoleRepository) : IUserRoleService
{
    private const string AdminRoleName = "Admin";
    private const string UserEntityType = "User";

    public Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        roleRepository.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<UserRoleRow>> GetUserRoleGridAsync(CancellationToken cancellationToken = default)
    {
        var users = await userRepository.GetAllAsync(cancellationToken);
        var rows = new List<UserRoleRow>(users.Count);

        foreach (var user in users)
        {
            var assignments = await userRoleRepository.GetForUserAsync(user.UserId, cancellationToken);
            rows.Add(new UserRoleRow(user.UserId, user.Username, user.FriendlyName, assignments.Select(a => a.RoleId).ToList()));
        }

        return rows;
    }

    public async Task<SetUserRolesResult> SetUserRolesAsync(Guid userId, IReadOnlyList<Guid> roleIds, Guid? actingUserId, CancellationToken cancellationToken = default)
    {
        // A directory user with no PT-LIMS account (no tblUsers row) must never get a role
        // assignment - blocks until Create User has run for them first.
        var users = await userRepository.GetAllAsync(cancellationToken);
        var user = users.FirstOrDefault(u => u.UserId == userId);
        if (user is null)
        {
            return new SetUserRolesResult(false, "This user does not have a PT-LIMS account yet. Create the account before assigning roles.");
        }

        var existing = await userRoleRepository.GetForUserAsync(userId, cancellationToken);
        var existingRoleIds = existing.Select(a => a.RoleId).ToHashSet();
        var desiredRoleIds = roleIds.ToHashSet();

        // Only blocks the CURRENT user removing their OWN Admin role. Legacy's
        // PreventAdminUncheck() blocked every user's Admin checkbox client-side only, for
        // everyone, with no server-side guard at all - this is a deliberate fix matching the
        // story's narrower, explicit scope, not a port of legacy's (undocumented, broader) behaviour.
        var roles = await roleRepository.GetAllAsync(cancellationToken);
        var adminRoleId = roles.FirstOrDefault(r => string.Equals(r.Name, AdminRoleName, StringComparison.OrdinalIgnoreCase))?.RoleId;

        if (adminRoleId is Guid adminId && existingRoleIds.Contains(adminId) && !desiredRoleIds.Contains(adminId) && actingUserId == userId)
        {
            return new SetUserRolesResult(false, "You cannot remove your own Admin access.");
        }

        var roleIdsToAdd = desiredRoleIds.Except(existingRoleIds).ToList();
        var userRoleIdsToRemove = existing.Where(a => !desiredRoleIds.Contains(a.RoleId)).Select(a => a.UserRoleId).ToList();

        if (roleIdsToAdd.Count == 0 && userRoleIdsToRemove.Count == 0)
        {
            return new SetUserRolesResult(true, null);
        }

        await userRoleRepository.ApplyRoleChangesAsync(userId, roleIdsToAdd, userRoleIdsToRemove, cancellationToken);

        return new SetUserRolesResult(true, null);
    }

    public async Task<UserRemoveResult> RemoveUserAsync(Guid userId, Guid? actingUserId, CancellationToken cancellationToken = default)
    {
        if (actingUserId == userId)
        {
            return new UserRemoveResult(false, "You cannot remove your own account.");
        }

        var users = await userRepository.GetAllAsync(cancellationToken);
        var user = users.FirstOrDefault(u => u.UserId == userId);
        var existing = await userRoleRepository.GetForUserAsync(userId, cancellationToken);

        // Role assignments, the user row and the audit entry go in one transaction - if the audit
        // write fails nothing is removed, so no unaudited removal is ever persisted.
        var auditEntry = new AuditEntry(actingUserId, "RemoveUser", UserEntityType, userId.ToString(), $"Username: {user?.Username}");
        await userRepository.DeleteWithAuditAsync(userId, existing.Select(a => a.UserRoleId).ToList(), auditEntry, cancellationToken);
        return new UserRemoveResult(true, null);
    }
}
