using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

internal sealed class FakeTestConsultantTabulationRepository : ITestConsultantTabulationRepository
{
    public List<TestConsultantDistributionEntity> Distributions { get; } = [];

    public Guid? DistributionsRequestedFor { get; private set; }

    public Dictionary<Guid, TestConsultantSchemeState> States { get; } = [];

    public Dictionary<(Guid SchemeId, bool Full), TestConsultantTabulationEntity> Tabulations { get; } = [];

    public Dictionary<Guid, string> Names { get; } = [];

    public HashSet<Guid> InactiveConsultants { get; } = [];

    public List<(Guid SchemeId, bool Full)> TabulationRequests { get; } = [];

    public Task<bool> IsInactiveAsync(Guid testConsultantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(InactiveConsultants.Contains(testConsultantId));

    public Task<IReadOnlyList<TestConsultantDistributionEntity>> GetDistributionsAsync(Guid testConsultantId, CancellationToken cancellationToken = default)
    {
        DistributionsRequestedFor = testConsultantId;
        return Task.FromResult<IReadOnlyList<TestConsultantDistributionEntity>>(Distributions);
    }

    public Task<TestConsultantTabulationEntity?> GetTabulationAsync(Guid monthlyDistributionSchemeId, bool fullTabulation, CancellationToken cancellationToken = default)
    {
        TabulationRequests.Add((monthlyDistributionSchemeId, fullTabulation));
        return Task.FromResult(Tabulations.GetValueOrDefault((monthlyDistributionSchemeId, fullTabulation)));
    }

    public Task<TestConsultantSchemeState?> GetSchemeStateAsync(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(States.GetValueOrDefault(monthlyDistributionSchemeId));

    public Task<string?> GetTestConsultantNameAsync(Guid testConsultantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Names.GetValueOrDefault(testConsultantId));
}
