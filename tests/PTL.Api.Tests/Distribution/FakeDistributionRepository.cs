using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

internal sealed class FakeDistributionRepository : IDistributionRepository
{
    public List<DistributionMonthSummaryEntity> Summaries { get; } = [];

    public List<DistributionMonthYearEntity> Years { get; } = [];

    public Task<IReadOnlyList<DistributionMonthSummaryEntity>> GetMonthlyDistributionSummariesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DistributionMonthSummaryEntity>>(Summaries);

    public Task<IReadOnlyList<DistributionMonthYearEntity>> GetDistributionYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DistributionMonthYearEntity>>(Years);
}
