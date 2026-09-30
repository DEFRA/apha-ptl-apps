using PTL.Core.Lookup;

namespace PTL.Core.WeightedPricingPlan;

public interface IWeightedPricingPlanRepository
{
    // spgaWeightedPricingYear - DISTINCT years that already have at least one pricing percentage
    // row (not every financial year in the system - matches the legacy year dropdown exactly).
    Task<IReadOnlyList<YearEntity>> GetYearsWithPercentagesAsync(CancellationToken cancellationToken = default);

    // spgPricingPercentageByYearId
    Task<IReadOnlyList<PricingPercentageEntity>> GetPercentagesForYearAsync(int yearId, CancellationToken cancellationToken = default);

    // sppRenewPricingPlan - idempotent copy-forward from (newYearId - 1) into newYearId; a no-op
    // if newYearId already has rows or if there's nothing to copy from.
    Task RenewAsync(int newYearId, CancellationToken cancellationToken = default);
}
