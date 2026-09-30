namespace PTL.Contracts.WeightedPricingPlan;

// Public API contract for GET /api/weighted-pricing-plan?yearId= - one row per grid cell.
public sealed record PricingPercentageResponse(int NumberOfDistributionsOnScheme, int NumberOfDistributionsChosen, int Weight);
