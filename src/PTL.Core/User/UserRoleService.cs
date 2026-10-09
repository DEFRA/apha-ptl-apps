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

        foreach (var roleId in desiredRoleIds.Except(existingRoleIds).ToList())
        {
            await userRoleRepository.AddAsync(Guid.NewGuid(), userId, roleId, cancellationToken);
        }

        // Materialized to a list first - awaiting RemoveAsync per iteration while enumerating
        // directly off `existing` risks an "enumeration operation may not execute" failure if a
        // repository implementation's existing collection is mutated by the remove itself.
        foreach (var assignment in existing.Where(a => !desiredRoleIds.Contains(a.RoleId)).ToList())
        {
            await userRoleRepository.RemoveAsync(assignment.UserRoleId, cancellationToken);
        }

        return new SetUserRolesResult(true, null);
    }

    public async Task<UserRemoveResult> RemoveUserAsync(Guid userId, Guid? actingUserId, CancellationToken cancellationToken = default)
    {
        if (actingUserId == userId)
        {
            return new UserRemoveResult(false, "You cannot remove your own account.");
        }

        var existing = await userRoleRepository.GetForUserAsync(userId, cancellationToken);
        foreach (var assignment in existing.ToList())
        {
            await userRoleRepository.RemoveAsync(assignment.UserRoleId, cancellationToken);
        }

        await userRepository.DeleteAsync(userId, cancellationToken);
        return new UserRemoveResult(true, null);
    }
}
