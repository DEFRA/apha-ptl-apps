namespace PTL.Core.PostagePricingPlan;

public interface IPostagePricingPlanService
{
    Task<PostagePricingPlanYearsResult> GetYearsAsync(CancellationToken cancellationToken = default);

    Task<PostagePricingPlanValidationResult> SetPriceAsync(Guid postageId, decimal ukPrice, decimal euPrice, decimal nonEuPrice, CancellationToken cancellationToken = default);

    // Always renews into "the next financial year" (computed server-side, never caller-supplied) -
    // matches legacy PostagePricingPlanCommand.Renew(mCurrentYears(1).YearId).
    Task<PostagePricingPlanRenewResult> RenewAsync(CancellationToken cancellationToken = default);
}
