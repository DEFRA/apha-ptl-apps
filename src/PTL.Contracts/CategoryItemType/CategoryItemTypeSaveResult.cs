namespace PTL.Contracts.CategoryItemType;

// Returned by PTL.ApiClient's CreateAsync/UpdateAsync/SetNoLongerInUseAsync so callers can surface
// validation errors (HTTP 400) or an in-use conflict (HTTP 409) without needing to catch an
// HttpRequestException - mirrors CountrySaveResult.
public sealed record CategoryItemTypeSaveResult(bool Success, CategoryItemTypeResponse? CategoryItemType, IReadOnlyDictionary<string, string[]> FieldErrors);
