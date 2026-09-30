using PTL.Core.Lookup;

namespace PTL.Core.WeightedPricingPlan;

public sealed class WeightedPricingPlanService(IWeightedPricingPlanRepository repository, ILookupService lookupService) : IWeightedPricingPlanService
{
    public async Task<WeightedPricingPlanYearsResult> GetYearsAsync(CancellationToken cancellationToken = default)
    {
        var availableYears = await repository.GetYearsWithPercentagesAsync(cancellationToken);

        // spgaYearCurrent orders "ORDER BY fldYear Asc" so [0] is always the current financial
        // year and [1] the next one - same positional assumption ContractController already
        // relies on for this same lookup.
        var currentYears = await lookupService.GetCurrentYearsAsync(cancellationToken);
        if (currentYears.Count < 2)
        {
            return new WeightedPricingPlanYearsResult(availableYears, false, null, null);
        }

        var currentYear = currentYears[0];
        var nextYear = currentYears[1];
        var currentYearHasPlan = availableYears.Any(y => y.YearId == currentYear.YearId);
        var nextYearHasPlan = availableYears.Any(y => y.YearId == nextYear.YearId);
        var canRenew = currentYearHasPlan && !nextYearHasPlan;

        return new WeightedPricingPlanYearsResult(availableYears, canRenew, nextYear.YearId, nextYear.Year);
    }

    public Task<IReadOnlyList<PricingPercentageEntity>> GetPercentagesForYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        repository.GetPercentagesForYearAsync(yearId, cancellationToken);

    public async Task<WeightedPricingPlanRenewResult> RenewAsync(CancellationToken cancellationToken = default)
    {
        var years = await GetYearsAsync(cancellationToken);
        if (!years.CanRenew || years.NextYearId is null)
        {
            return new WeightedPricingPlanRenewResult(false, "No weighted pricing percentages have been entered for the current financial year, so the plan cannot be renewed.");
        }

        await repository.RenewAsync(years.NextYearId.Value, cancellationToken);
        return new WeightedPricingPlanRenewResult(true, $"The weighted pricing plan has successfully been renewed for the financial year {years.NextYearLabel}.");
    }
}
