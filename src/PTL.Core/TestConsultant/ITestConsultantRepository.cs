using PTL.Core.Audit;

namespace PTL.Core.TestConsultant;

public interface ITestConsultantRepository
{
    /// <summary>Reads the test consultant row matching a CIDM contact id via <c>spgTestConsultantBySsoId</c>.</summary>
    Task<TestConsultant?> GetBySsoIdExtAsync(Guid ssoIdExt, CancellationToken cancellationToken = default);

    /// <summary>Reads the test consultant row matching an email address via <c>spgTestConsultantByEmail</c>, used only as a CIDM fallback.</summary>
    Task<TestConsultant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>The External Test Consultant Management grid (spgaExtTestConsultants).</summary>
    Task<IReadOnlyList<TestConsultant>> GetAllAsync(CancellationToken cancellationToken = default);

    // A non-null auditEntry is written in the same transaction as the change (System Administration
    // changes); null is for system-initiated updates such as the SSO link at external sign-in.
    Task<TestConsultant> CreateAsync(TestConsultant testConsultant, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default);
    Task<TestConsultant?> UpdateAsync(TestConsultant testConsultant, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default);

    // spdExtTestConsultant. False when no row was deleted.
    Task<bool> DeleteAsync(Guid externalTestConsultantId, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default);
}
