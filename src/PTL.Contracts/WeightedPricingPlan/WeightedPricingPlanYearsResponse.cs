using PTL.Contracts.Lookup;

namespace PTL.Contracts.WeightedPricingPlan;

// Public API contract for GET /api/weighted-pricing-plan/years. CanRenew/NextYearId/NextYearLabel
// are computed once server-side (PTL.Core.WeightedPricingPlan.WeightedPricingPlanService) so
// every caller (including PTL.InternalWeb) reads the same answer instead of re-deriving it.
public sealed record WeightedPricingPlanYearsResponse(IReadOnlyList<YearResponse> AvailableYears, bool CanRenew, int? NextYearId, string? NextYearLabel);
