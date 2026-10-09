namespace PTL.Contracts.Country;

// Returned by PTL.ApiClient's CreateCountryAsync/UpdateCountryAsync so callers can surface
// validation errors (HTTP 400) without needing to catch an HttpRequestException - mirrors
// GroupAddressSaveResult/PostagePricingPlanSaveResult.
public sealed record CountrySaveResult(bool Success, CountryResponse? Country, IReadOnlyDictionary<string, string[]> FieldErrors);
