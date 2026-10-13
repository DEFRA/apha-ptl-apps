using PTL.Core.Lookup;

namespace PTL.Core.PostagePricingPlan;

public sealed class PostagePricingPlanService(IPostagePricingPlanRepository repository, ILookupService lookupService) : IPostagePricingPlanService
{
    public async Task<PostagePricingPlanYearsResult> GetYearsAsync(CancellationToken cancellationToken = default)
    {
        var availableYears = await repository.GetYearsWithPlanAsync(cancellationToken);

        // spgaYearCurrent orders "ORDER BY fldYear Asc" so [0] is always the current financial
        // year and [1] the next one - same positional assumption WeightedPricingPlanService relies on.
        var currentYears = await lookupService.GetCurrentYearsAsync(cancellationToken);
        if (currentYears.Count < 2)
        {
            return new PostagePricingPlanYearsResult(availableYears, false, null, null);
        }

        var currentYear = currentYears[0];
        var nextYear = currentYears[1];
        var currentYearHasPlan = availableYears.Any(y => y.YearId == currentYear.YearId);
        var nextYearHasPlan = availableYears.Any(y => y.YearId == nextYear.YearId);
        var canRenew = currentYearHasPlan && !nextYearHasPlan;

        return new PostagePricingPlanYearsResult(availableYears, canRenew, nextYear.YearId, nextYear.Year);
    }

    public async Task<PostagePricingPlanValidationResult> SetPriceAsync(Guid postageId, decimal ukPrice, decimal euPrice, decimal nonEuPrice, CancellationToken cancellationToken = default)
    {
        // Fetch-then-save, matching legacy PostagePricingPlan.FetchPostagePricingPlan(id) before
        // .Save(): confirms the row exists and sources its real YearId server-side, so a
        // caller-supplied YearId can never silently move a row to a different financial year.
        var existing = await repository.GetByIdAsync(postageId, cancellationToken);
        if (existing is null)
        {
            return new PostagePricingPlanValidationResult(false, [new PostagePricingPlanValidationError("PostageId", "Postage pricing plan not found.")]);
        }

        var validation = PostagePricingPlanValidator.Validate(ukPrice, euPrice, nonEuPrice);
        if (!validation.IsValid)
        {
            return validation;
        }

        await repository.SetPriceAsync(postageId, existing.YearId, ukPrice, euPrice, nonEuPrice, cancellationToken);
        return validation;
    }

    public async Task<PostagePricingPlanRenewResult> RenewAsync(CancellationToken cancellationToken = default)
    {
        var years = await GetYearsAsync(cancellationToken);
        if (!years.CanRenew || years.NextYearId is null)
        {
            return new PostagePricingPlanRenewResult(false, "No postage pricing plan has been entered for the current financial year, so the plan cannot be renewed.");
        }

        await repository.RenewAsync(years.NextYearId.Value, cancellationToken);
        return new PostagePricingPlanRenewResult(true, $"The postage pricing plan has successfully been renewed for the financial year {years.NextYearLabel}.");
    }
}
