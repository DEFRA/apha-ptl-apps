namespace PTL.Core.WeightedPricingPlan;

public interface IWeightedPricingPlanService
{
    Task<WeightedPricingPlanYearsResult> GetYearsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PricingPercentageEntity>> GetPercentagesForYearAsync(int yearId, CancellationToken cancellationToken = default);

    // Always renews into "the next financial year" (computed server-side, never caller-supplied) -
    // matches legacy PricingPlanCommand.Renew(mCurrentYears(1).YearId), which never let the user
    // pick an arbitrary target year.
    Task<WeightedPricingPlanRenewResult> RenewAsync(CancellationToken cancellationToken = default);
}
