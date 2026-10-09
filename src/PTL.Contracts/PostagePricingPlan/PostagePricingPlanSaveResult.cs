namespace PTL.Contracts.PostagePricingPlan;

// Returned by PTL.ApiClient's SetPriceAsync so callers can surface validation errors (HTTP 400)
// without needing to catch an HttpRequestException - mirrors AdministrationChargeSaveResult.
public sealed record PostagePricingPlanSaveResult(bool Success, IReadOnlyDictionary<string, string[]> FieldErrors);
