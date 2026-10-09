namespace PTL.Contracts.TestConsultant;

// Mirrors CountrySaveResult/CreateUserSaveResult - lets callers surface validation errors
// (HTTP 400) without needing to catch an HttpRequestException.
public sealed record ExternalTestConsultantSaveResult(bool Success, ExternalTestConsultantResponse? TestConsultant, IReadOnlyDictionary<string, string[]> FieldErrors);
