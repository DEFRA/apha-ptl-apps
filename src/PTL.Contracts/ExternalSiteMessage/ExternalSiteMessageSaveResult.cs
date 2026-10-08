namespace PTL.Contracts.ExternalSiteMessage;

// Returned by PTL.ApiClient's UpdateAsync so callers can surface validation errors (HTTP 400)
// without needing to catch an HttpRequestException - mirrors GroupAddressSaveResult/CountrySaveResult.
public sealed record ExternalSiteMessageSaveResult(bool Success, ExternalSiteMessageResponse? ExternalSiteMessage, IReadOnlyDictionary<string, string[]> FieldErrors);
