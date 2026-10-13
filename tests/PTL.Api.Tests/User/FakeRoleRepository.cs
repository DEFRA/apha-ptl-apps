using PTL.Core.User;

namespace PTL.Api.Tests.User;

internal sealed class FakeRoleRepository : IRoleRepository
{
    public List<Role> Roles { get; set; } = [];

    public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Role>>(Roles);
}

internal sealed class FakeUserRoleRepository : IUserRoleRepository
{
    public Dictionary<Guid, List<UserRoleAssignment>> AssignmentsByUserId { get; set; } = [];
    public List<(Guid UserRoleId, Guid UserId, Guid RoleId)> Added { get; } = [];
    public List<Guid> Removed { get; } = [];

    public Task<IReadOnlyList<UserRoleAssignment>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserRoleAssignment>>(AssignmentsByUserId.GetValueOrDefault(userId, []));

    public Task AddAsync(Guid userRoleId, Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        Added.Add((userRoleId, userId, roleId));
        AssignmentsByUserId.TryAdd(userId, []);
        AssignmentsByUserId[userId].Add(new UserRoleAssignment { UserRoleId = userRoleId, UserId = userId, RoleId = roleId });
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid userRoleId, CancellationToken cancellationToken = default)
    {
        Removed.Add(userRoleId);
        foreach (var list in AssignmentsByUserId.Values)
        {
            list.RemoveAll(a => a.UserRoleId == userRoleId);
        }

        return Task.CompletedTask;
    }
}
