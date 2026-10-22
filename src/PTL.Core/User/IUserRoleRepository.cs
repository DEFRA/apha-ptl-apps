namespace PTL.Core.User;

public interface IUserRoleRepository
{
    // spgUserRoleList
    Task<IReadOnlyList<UserRoleAssignment>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    // spiUserRole
    Task AddAsync(Guid userRoleId, Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    // spdUserRole
    Task RemoveAsync(Guid userRoleId, CancellationToken cancellationToken = default);

    // Applies every add/remove in roleIdsToAdd/userRoleIdsToRemove in ONE DB transaction
    // (spiUserRole/spdUserRole per change) - unchanged legacy stored procedures only, matching
    // legacy's UserRoleCollection.Update() which never audited role changes.
    Task ApplyRoleChangesAsync(
        Guid userId,
        IReadOnlyList<Guid> roleIdsToAdd,
        IReadOnlyList<Guid> userRoleIdsToRemove,
        CancellationToken cancellationToken = default);
}
