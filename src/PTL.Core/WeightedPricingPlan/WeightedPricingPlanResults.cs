using PTL.Core.Lookup;

namespace PTL.Core.WeightedPricingPlan;

// Years with a configured plan, plus whether the plan can be renewed for the next financial year
// right now - mirrors legacy WeightedPricingPlan.aspx.vb's SetRenewBtn(): renewal is only offered
// once the current year has a plan and the next year doesn't have one yet.
public sealed record WeightedPricingPlanYearsResult(IReadOnlyList<YearEntity> AvailableYears, bool CanRenew, int? NextYearId, string? NextYearLabel);

public sealed record WeightedPricingPlanRenewResult(bool Success, string Message);
