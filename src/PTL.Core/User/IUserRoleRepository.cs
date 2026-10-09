namespace PTL.Core.User;

public interface IUserRoleRepository
{
    // spgUserRoleList
    Task<IReadOnlyList<UserRoleAssignment>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    // spiUserRole
    Task AddAsync(Guid userRoleId, Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    // spdUserRole
    Task RemoveAsync(Guid userRoleId, CancellationToken cancellationToken = default);
}
