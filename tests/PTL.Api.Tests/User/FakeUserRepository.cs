using PTL.Core.Audit;
using PTL.Core.User;

namespace PTL.Api.Tests.User;

internal sealed class FakeUserRepository : IUserRepository
{
    public List<PTL.Core.User.User> Users { get; set; } = [];
    public List<PTL.Core.User.User> Created { get; } = [];
    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<PTL.Core.User.User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PTL.Core.User.User>>(Users);

    public Task CreateAsync(PTL.Core.User.User user, CancellationToken cancellationToken = default)
    {
        Created.Add(user);
        Users.Add(user);
        return Task.CompletedTask;
    }

    // When true, simulates the audit write failing - mimics the real transaction's rollback by
    // throwing BEFORE the user row is added, so a test can assert nothing was persisted.
    public bool ThrowOnAudit { get; set; }
    public AuditEntry? LastAuditEntry { get; private set; }

    public Task CreateWithAuditAsync(PTL.Core.User.User user, AuditEntry auditEntry, CancellationToken cancellationToken = default)
    {
        if (ThrowOnAudit)
        {
            throw new InvalidOperationException("Simulated audit trail write failure.");
        }

        Created.Add(user);
        Users.Add(user);
        LastAuditEntry = auditEntry;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(userId);
        Users.RemoveAll(u => u.UserId == userId);
        return Task.CompletedTask;
    }

    public List<Guid> RemovedUserRoleIds { get; } = [];

    public Task DeleteWithAuditAsync(Guid userId, IReadOnlyList<Guid> userRoleIds, AuditEntry auditEntry, CancellationToken cancellationToken = default)
    {
        if (ThrowOnAudit)
        {
            throw new InvalidOperationException("Simulated audit trail write failure.");
        }

        RemovedUserRoleIds.AddRange(userRoleIds);
        Deleted.Add(userId);
        Users.RemoveAll(u => u.UserId == userId);
        LastAuditEntry = auditEntry;
        return Task.CompletedTask;
    }

    public List<PTL.Core.User.User> TestConsultants { get; set; } = [];
    public List<(Guid UserId, string Department, bool IsInactive, DateTime? InactiveDate)> DepartmentUpdates { get; } = [];

    public Task<IReadOnlyList<PTL.Core.User.User>> GetTestConsultantsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PTL.Core.User.User>>(TestConsultants);

    public Task UpdateDepartmentAsync(Guid userId, string department, bool isInactive, DateTime? inactiveDate, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default)
    {
        if (ThrowOnAudit)
        {
            throw new InvalidOperationException("Simulated audit trail write failure.");
        }

        DepartmentUpdates.Add((userId, department, isInactive, inactiveDate));
        LastAuditEntry = auditEntry;
        return Task.CompletedTask;
    }
}

