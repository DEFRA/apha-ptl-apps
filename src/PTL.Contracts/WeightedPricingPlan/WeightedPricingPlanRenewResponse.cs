namespace PTL.Contracts.WeightedPricingPlan;

// Public API contract for POST /api/weighted-pricing-plan/renew.
public sealed record WeightedPricingPlanRenewResponse(bool Success, string Message);
