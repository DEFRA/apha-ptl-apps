using PTL.Core.Audit;

namespace PTL.Core.User;

// The acting user is resolved by PTL.InternalWeb (from the signed-in Entra claims) and passed in
// explicitly as actingUserId, not read from IActingUserAccessor like the other services. The
// self-removal and self-Admin-removal guards compare against it, and PTL.Api does not yet validate
// who the caller is, so it is a self-reported value.
public sealed class UserRoleService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUserRoleRepository userRoleRepository) : IUserRoleService
{
    private const string AdminRoleName = "Admin";
    private const string UserEntityType = "User";

    // Defence-in-depth: PTL.Api's UserRoleController already rejects oversized batch payloads
    // before this service is reached, but the service must not itself trust that every caller
    // enforces those limits - an unbounded rows/RoleIds count here would let any caller force
    // unbounded dictionary/hash-set allocation (a memory-exhaustion DoS), so the same caps are
    // re-asserted at the service boundary.
    private const int MaxBatchItems = 2_000;
    private const int MaxRoleIdsPerItem = 500;

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

        // Only blocks the CURRENT user removing their OWN Admin role - legacy's PreventAdminUncheck()
        // greyed out the Admin checkbox on the signed-in user's own row (UserRoles.aspx) and nothing else.
        // Here the check is server-side, so it holds whatever the form posts.
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

        var roleNamesById = roles.ToDictionary(r => r.RoleId, r => r.Name);
        var addedNames = roleIdsToAdd.Select(id => roleNamesById.GetValueOrDefault(id, id.ToString()));
        var removedNames = existing.Where(a => userRoleIdsToRemove.Contains(a.UserRoleId)).Select(a => roleNamesById.GetValueOrDefault(a.RoleId, a.RoleId.ToString()));
        var details = $"Added: [{string.Join(", ", addedNames)}]; Removed: [{string.Join(", ", removedNames)}]";

        var auditEntry = new AuditEntry(actingUserId, "SetUserRoles", UserEntityType, userId.ToString(), details);

        // Audited in one DB transaction - if the audit write fails, every add/remove in this
        // batch rolls back too, so no unaudited role change is ever persisted.
        await userRoleRepository.ApplyRoleChangesWithAuditAsync(userId, roleIdsToAdd, userRoleIdsToRemove, auditEntry, cancellationToken);

        return new SetUserRolesResult(true, null);
    }

    public async Task<IReadOnlyList<SetUserRolesItemResult>> SetUserRolesBatchAsync(
        IReadOnlyList<(Guid UserId, IReadOnlyList<Guid> RoleIds)> rows,
        Guid? actingUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count > MaxBatchItems)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), $"No more than {MaxBatchItems} rows can be saved in a single request.");
        }

        if (rows.Any(r => r.RoleIds is null))
        {
            throw new ArgumentException("RoleIds must be provided for every row.", nameof(rows));
        }

        if (rows.Any(r => r.RoleIds.Count > MaxRoleIdsPerItem))
        {
            throw new ArgumentOutOfRangeException(nameof(rows), $"No more than {MaxRoleIdsPerItem} role ids can be submitted for a single user.");
        }

        // Fetched once for the whole batch rather than once per row - the N+1 full-table
        // re-fetch per row (plus a separate connection/transaction per row) was the main cause of
        // the intermittent gateway timeouts on larger grids.
        var users = await userRepository.GetAllAsync(cancellationToken);
        var usersById = users.ToDictionary(u => u.UserId);
        var roles = await roleRepository.GetAllAsync(cancellationToken);
        var roleNamesById = roles.ToDictionary(r => r.RoleId, r => r.Name);
        var adminRoleId = roles.FirstOrDefault(r => string.Equals(r.Name, AdminRoleName, StringComparison.OrdinalIgnoreCase))?.RoleId;

        var results = new List<SetUserRolesItemResult>(rows.Count);
        var changeSets = new List<UserRoleChangeSet>();

        foreach (var (userId, roleIds) in rows)
        {
            if (!usersById.ContainsKey(userId))
            {
                results.Add(new SetUserRolesItemResult(userId, false, "This user does not have a PT-LIMS account yet. Create the account before assigning roles."));
                continue;
            }

            var existing = await userRoleRepository.GetForUserAsync(userId, cancellationToken);
            var existingRoleIds = existing.Select(a => a.RoleId).ToHashSet();
            var desiredRoleIds = roleIds.ToHashSet();

            if (adminRoleId is Guid adminId && existingRoleIds.Contains(adminId) && !desiredRoleIds.Contains(adminId) && actingUserId == userId)
            {
                results.Add(new SetUserRolesItemResult(userId, false, "You cannot remove your own Admin access."));
                continue;
            }

            var roleIdsToAdd = desiredRoleIds.Except(existingRoleIds).ToList();
            var userRoleIdsToRemove = existing.Where(a => !desiredRoleIds.Contains(a.RoleId)).Select(a => a.UserRoleId).ToList();

            results.Add(new SetUserRolesItemResult(userId, true, null));

            if (roleIdsToAdd.Count == 0 && userRoleIdsToRemove.Count == 0)
            {
                continue;
            }

            var addedNames = roleIdsToAdd.Select(id => roleNamesById.GetValueOrDefault(id, id.ToString()));
            var removedNames = existing.Where(a => userRoleIdsToRemove.Contains(a.UserRoleId)).Select(a => roleNamesById.GetValueOrDefault(a.RoleId, a.RoleId.ToString()));
            var details = $"Added: [{string.Join(", ", addedNames)}]; Removed: [{string.Join(", ", removedNames)}]";
            var auditEntry = new AuditEntry(actingUserId, "SetUserRoles", UserEntityType, userId.ToString(), details);

            changeSets.Add(new UserRoleChangeSet(userId, roleIdsToAdd, userRoleIdsToRemove, auditEntry));
        }

        if (changeSets.Count > 0)
        {
            // Every user's add/remove diff plus audit entry is applied in ONE DB transaction -
            // one round trip for the whole grid instead of one per user.
            await userRoleRepository.ApplyBatchRoleChangesWithAuditAsync(changeSets, cancellationToken);
        }

        return results;
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
