namespace PTL.Contracts.PostagePricingPlan;

// Public API contract for PUT /api/postage-pricing-plan/price. YearId is deliberately absent -
// the server sources it from the existing row (never from the client) so a request can never move
// a row to a different financial year; see PostagePricingPlanService.SetPriceAsync.
public sealed record UpdatePostagePricingPlanPriceRequest(Guid PostageId, decimal UKPrice, decimal EUPrice, decimal NonEUPrice);
