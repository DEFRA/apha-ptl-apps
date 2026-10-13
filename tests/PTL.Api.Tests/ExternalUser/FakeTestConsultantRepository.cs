using PTL.Core.TestConsultant;
using CoreTestConsultant = PTL.Core.TestConsultant.TestConsultant;

namespace PTL.Api.Tests.ExternalUser;

internal sealed class FakeTestConsultantRepository : ITestConsultantRepository
{
    private readonly Dictionary<Guid, CoreTestConsultant> _testConsultants = [];

    public List<CoreTestConsultant> UpdateCalls { get; } = [];

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

    public Task<CoreTestConsultant> CreateAsync(CoreTestConsultant testConsultant, CancellationToken cancellationToken = default)
    {
        testConsultant.ExternalTestConsultantId = testConsultant.ExternalTestConsultantId == Guid.Empty ? Guid.NewGuid() : testConsultant.ExternalTestConsultantId;
        _testConsultants[testConsultant.ExternalTestConsultantId] = testConsultant;
        return Task.FromResult(testConsultant);
    }

    public Task<CoreTestConsultant?> UpdateAsync(CoreTestConsultant testConsultant, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add(testConsultant);
        if (!_testConsultants.ContainsKey(testConsultant.ExternalTestConsultantId))
        {
            return Task.FromResult<CoreTestConsultant?>(null);
        }

        _testConsultants[testConsultant.ExternalTestConsultantId] = testConsultant;
        return Task.FromResult<CoreTestConsultant?>(testConsultant);
    }

    public Task<IReadOnlyList<CoreTestConsultant>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CoreTestConsultant>>(_testConsultants.Values.ToList());
}
