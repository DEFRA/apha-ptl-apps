namespace PTL.Contracts.User;

// Returned by PTL.ApiClient's CreateUserAsync so callers can surface validation errors (HTTP 400)
// without needing to catch an HttpRequestException - mirrors CountrySaveResult/GroupAddressSaveResult.
public sealed record CreateUserSaveResult(bool Success, UserResponse? User, IReadOnlyDictionary<string, string[]> FieldErrors);
