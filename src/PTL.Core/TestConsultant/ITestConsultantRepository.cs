using PTL.Core.Audit;

namespace PTL.Core.TestConsultant;

public interface ITestConsultantRepository
{
    /// <summary>The External Test Consultant Management grid (spgaExtTestConsultants).</summary>
    Task<IReadOnlyList<TestConsultant>> GetAllAsync(CancellationToken cancellationToken = default);

    // A non-null auditEntry is written in the same transaction as the change (System Administration
    // changes); null is for system-initiated changes.
    Task<TestConsultant> CreateAsync(TestConsultant testConsultant, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default);
    Task<TestConsultant?> UpdateAsync(TestConsultant testConsultant, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default);

    // spdExtTestConsultant. False when no row was deleted.
    Task<bool> DeleteAsync(Guid externalTestConsultantId, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default);
}
