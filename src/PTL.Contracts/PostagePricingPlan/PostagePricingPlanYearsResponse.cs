using PTL.Contracts.Lookup;

namespace PTL.Contracts.PostagePricingPlan;

// Public API contract for GET /api/postage-pricing-plan/years.
public sealed record PostagePricingPlanYearsResponse(IReadOnlyList<YearResponse> AvailableYears, bool CanRenew, int? NextYearId, string? NextYearLabel);
