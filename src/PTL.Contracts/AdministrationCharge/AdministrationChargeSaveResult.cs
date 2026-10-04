namespace PTL.Contracts.AdministrationCharge;

// Returned by PTL.ApiClient's SetPriceAsync so callers can surface validation errors (HTTP 400)
// without needing to catch an HttpRequestException - mirrors ContractSaveResult.
public sealed record AdministrationChargeSaveResult(bool Success, AdministrationChargeCurrencyPriceResponse? Price, IReadOnlyDictionary<string, string[]> FieldErrors);
