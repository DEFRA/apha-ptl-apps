using PTL.Core.Lookup;

namespace PTL.Core.PostagePricingPlan;

public interface IPostagePricingPlanRepository
{
    // spgaPostagePricingYears - DISTINCT years that already have at least one postage pricing row
    // (not every financial year in the system - matches the legacy year dropdown exactly).
    Task<IReadOnlyList<YearEntity>> GetYearsWithPlanAsync(CancellationToken cancellationToken = default);

    // spgPostageByPostageID - used to verify a row exists and to source its real YearId server-side
    // before an update, rather than trusting a client-supplied YearId.
    Task<PostagePricingPlanEntity?> GetByIdAsync(Guid postageId, CancellationToken cancellationToken = default);

    // spuPostage
    Task SetPriceAsync(Guid postageId, int yearId, decimal ukPrice, decimal euPrice, decimal nonEuPrice, CancellationToken cancellationToken = default);

    // sppRenewPostagePricingPlan - idempotent copy-forward from (newYearId - 1) into newYearId; a
    // no-op if newYearId already has rows or if there's nothing to copy from.
    Task RenewAsync(int newYearId, CancellationToken cancellationToken = default);
}
