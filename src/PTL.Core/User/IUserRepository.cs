using PTL.Core.Audit;

namespace PTL.Core.User;

public interface IUserRepository
{
    // spgaUser
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    // spiUser
    Task CreateAsync(User user, CancellationToken cancellationToken = default);

    // spiUser + spiLog, in one transaction - if the audit write fails, the
    // user row is never persisted either.
    Task CreateWithAuditAsync(User user, AuditEntry auditEntry, CancellationToken cancellationToken = default);

    // spdUser
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default);

    // spdUserRole for each of the user's role assignments, then spdUser, then
    // spiLog, in one transaction - if the audit write fails, the user and
    // their role assignments all remain.
    Task DeleteWithAuditAsync(Guid userId, IReadOnlyList<Guid> userRoleIds, AuditEntry auditEntry, CancellationToken cancellationToken = default);

    // spgaUserTestConsultant - the Internal Test Consultant Department Management grid.
    Task<IReadOnlyList<User>> GetTestConsultantsAsync(CancellationToken cancellationToken = default);

    // spuUserDept - updates Department/IsInactive/InactiveDate together, matching the stored
    // procedure's own parameters exactly. Audited in the same transaction when auditEntry is given.
    Task UpdateDepartmentAsync(Guid userId, string department, bool isInactive, DateTime? inactiveDate, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default);
}
