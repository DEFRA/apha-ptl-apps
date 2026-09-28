using PTL.Core.Lookup;
using PTL.Core.WeightedPricingPlan;

namespace PTL.Api.Tests.WeightedPricingPlan;

// In-memory IWeightedPricingPlanRepository test double so WeightedPricingPlanService can be
// tested without a real database or the spgaWeightedPricingYear/spgPricingPercentageByYearId/
// sppRenewPricingPlan stored procedures.
internal sealed class FakeWeightedPricingPlanRepository : IWeightedPricingPlanRepository
{
    public IReadOnlyList<YearEntity> YearsWithPercentages { get; set; } = [];
    public Dictionary<int, IReadOnlyList<PricingPercentageEntity>> PercentagesByYear { get; set; } = [];
    public List<int> RenewedYearIds { get; } = [];

    public Task<IReadOnlyList<YearEntity>> GetYearsWithPercentagesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(YearsWithPercentages);

    public Task<IReadOnlyList<PricingPercentageEntity>> GetPercentagesForYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PercentagesByYear.GetValueOrDefault(yearId, []));

    public Task RenewAsync(int newYearId, CancellationToken cancellationToken = default)
    {
        RenewedYearIds.Add(newYearId);
        YearsWithPercentages = [.. YearsWithPercentages, new YearEntity { YearId = newYearId, Year = $"{newYearId}/{(newYearId + 1) % 100:00}" }];
        return Task.CompletedTask;
    }
}
