namespace PTL.Contracts.PostagePricingPlan;

// Public API contract for POST /api/postage-pricing-plan/renew.
public sealed record PostagePricingPlanRenewResponse(bool Success, string Message);
