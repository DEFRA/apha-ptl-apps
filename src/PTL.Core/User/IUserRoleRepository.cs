using PTL.Core.Audit;

namespace PTL.Core.User;

public interface IUserRoleRepository
{
    // spgUserRoleList
    Task<IReadOnlyList<UserRoleAssignment>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    // spiUserRole
    Task AddAsync(Guid userRoleId, Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    // spdUserRole
    Task RemoveAsync(Guid userRoleId, CancellationToken cancellationToken = default);

    // Applies every add/remove in roleIdsToAdd/userRoleIdsToRemove plus the audit entry in ONE DB
    // transaction (spiUserRole/spdUserRole per change, then spiLog) - if the
    // audit write fails, the whole transaction (including every role change) rolls back, so no
    // unaudited role change is ever persisted.
    Task ApplyRoleChangesWithAuditAsync(
        Guid userId,
        IReadOnlyList<Guid> roleIdsToAdd,
        IReadOnlyList<Guid> userRoleIdsToRemove,
        AuditEntry auditEntry,
        CancellationToken cancellationToken = default);

    // Same as ApplyRoleChangesWithAuditAsync but applies every user's add/remove set plus every
    // user's audit entry in ONE connection/transaction, instead of opening a new connection and
    // transaction per user - used by the Manage User Roles "save the whole grid" action so saving
    // many users is a single DB round trip rather than N, avoiding the gateway timeouts that the
    // per-user loop caused on larger grids.
    Task ApplyBatchRoleChangesWithAuditAsync(
        IReadOnlyList<UserRoleChangeSet> changeSets,
        CancellationToken cancellationToken = default);
}

// One user's add/remove role diff plus its audit entry, applied as part of a batch save.
public sealed record UserRoleChangeSet(
    Guid UserId,
    IReadOnlyList<Guid> RoleIdsToAdd,
    IReadOnlyList<Guid> UserRoleIdsToRemove,
    AuditEntry AuditEntry);
