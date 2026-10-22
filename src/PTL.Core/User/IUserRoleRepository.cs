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
}
