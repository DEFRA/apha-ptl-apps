using PTL.Core.Audit;
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

    // When true, simulates the audit write failing - mimics the real transaction's rollback by
    // throwing BEFORE any add/remove is applied, so a test can assert nothing was persisted.
    public bool ThrowOnAudit { get; set; }
    public AuditEntry? LastAuditEntry { get; private set; }

    public Task ApplyRoleChangesWithAuditAsync(
        Guid userId,
        IReadOnlyList<Guid> roleIdsToAdd,
        IReadOnlyList<Guid> userRoleIdsToRemove,
        AuditEntry auditEntry,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnAudit)
        {
            throw new InvalidOperationException("Simulated audit trail write failure.");
        }

        foreach (var roleId in roleIdsToAdd)
        {
            Added.Add((Guid.NewGuid(), userId, roleId));
            AssignmentsByUserId.TryAdd(userId, []);
            AssignmentsByUserId[userId].Add(new UserRoleAssignment { UserRoleId = Added[^1].UserRoleId, UserId = userId, RoleId = roleId });
        }

        foreach (var userRoleId in userRoleIdsToRemove)
        {
            Removed.Add(userRoleId);
            foreach (var list in AssignmentsByUserId.Values)
            {
                list.RemoveAll(a => a.UserRoleId == userRoleId);
            }
        }

        LastAuditEntry = auditEntry;
        return Task.CompletedTask;
    }
}
