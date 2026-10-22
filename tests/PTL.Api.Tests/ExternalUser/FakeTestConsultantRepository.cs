using PTL.Core.Audit;
using PTL.Core.TestConsultant;
using CoreTestConsultant = PTL.Core.TestConsultant.TestConsultant;

namespace PTL.Api.Tests.ExternalUser;

internal sealed class FakeTestConsultantRepository : ITestConsultantRepository
{
    private readonly Dictionary<Guid, CoreTestConsultant> _testConsultants = [];

    public List<CoreTestConsultant> UpdateCalls { get; } = [];

    public List<Guid> DeleteCalls { get; } = [];

    // Audit entry passed to the most recent CreateAsync/UpdateAsync (null when none was supplied).
    public AuditEntry? LastAuditEntry { get; private set; }

    public void Seed(CoreTestConsultant testConsultant) =>
        _testConsultants[testConsultant.ExternalTestConsultantId] = testConsultant;

    public Task<CoreTestConsultant?> GetBySsoIdExtAsync(Guid ssoIdExt, CancellationToken cancellationToken = default)
    {
        var match = _testConsultants.Values.FirstOrDefault(t => t.SsoIdExt == ssoIdExt);
        return Task.FromResult(match);
    }

    public Task<CoreTestConsultant?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var match = _testConsultants.Values.FirstOrDefault(t => string.Equals(t.Email, email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task<CoreTestConsultant> CreateAsync(CoreTestConsultant testConsultant, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default)
    {
        LastAuditEntry = auditEntry;
        testConsultant.ExternalTestConsultantId = testConsultant.ExternalTestConsultantId == Guid.Empty ? Guid.NewGuid() : testConsultant.ExternalTestConsultantId;
        _testConsultants[testConsultant.ExternalTestConsultantId] = testConsultant;
        return Task.FromResult(testConsultant);
    }

    public Task<CoreTestConsultant?> UpdateAsync(CoreTestConsultant testConsultant, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add(testConsultant);
        LastAuditEntry = auditEntry;
        if (!_testConsultants.ContainsKey(testConsultant.ExternalTestConsultantId))
        {
            return Task.FromResult<CoreTestConsultant?>(null);
        }

        _testConsultants[testConsultant.ExternalTestConsultantId] = testConsultant;
        return Task.FromResult<CoreTestConsultant?>(testConsultant);
    }

    public Task<bool> DeleteAsync(Guid externalTestConsultantId, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(externalTestConsultantId);
        LastAuditEntry = auditEntry;
        return Task.FromResult(_testConsultants.Remove(externalTestConsultantId));
    }

    public Task<IReadOnlyList<CoreTestConsultant>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CoreTestConsultant>>(_testConsultants.Values.ToList());
}
