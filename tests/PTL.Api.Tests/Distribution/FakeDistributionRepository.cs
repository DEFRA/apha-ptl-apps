using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

internal sealed class FakeDistributionRepository : IDistributionRepository
{
    public List<DistributionMonthSummaryEntity> Summaries { get; } = [];

    public List<DistributionMonthYearEntity> Years { get; } = [];

    // Keyed by (yearId, monthId) - null means "not initialised", matching GetMonthlyDistributionSchedulesAsync's contract.
    public Dictionary<(int YearId, int MonthId), (Guid MonthlyDistributionId, List<MonthlyDistributionSchemeEntity> Schemes)> Schedules { get; } = [];

    public List<MonthlyDistributionSchemeEntity> UpdatedSchemes { get; } = [];

    public Task<IReadOnlyList<DistributionMonthSummaryEntity>> GetMonthlyDistributionSummariesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DistributionMonthSummaryEntity>>(Summaries);

    public Task<IReadOnlyList<DistributionMonthYearEntity>> GetDistributionYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DistributionMonthYearEntity>>(Years);

    public Task<(Guid MonthlyDistributionId, IReadOnlyList<MonthlyDistributionSchemeEntity> Schemes)?> GetMonthlyDistributionSchedulesAsync(
        int yearId, int monthId, CancellationToken cancellationToken = default)
    {
        if (!Schedules.TryGetValue((yearId, monthId), out var schedule))
        {
            return Task.FromResult<(Guid, IReadOnlyList<MonthlyDistributionSchemeEntity>)?>(null);
        }

        return Task.FromResult<(Guid, IReadOnlyList<MonthlyDistributionSchemeEntity>)?>((schedule.MonthlyDistributionId, schedule.Schemes));
    }

    public Task UpdateMonthlyDistributionSchemesAsync(IReadOnlyList<MonthlyDistributionSchemeEntity> schemes, CancellationToken cancellationToken = default)
    {
        UpdatedSchemes.AddRange(schemes);
        return Task.CompletedTask;
    }
}
