namespace PTL.Contracts.Contract;

// Returned by PTL.ApiClient's CreateContractAsync/UpdateContractAsync so callers can surface
// validation errors (HTTP 400) without needing to catch an HttpRequestException. FieldErrors is
// keyed by the ContractFormViewModel property name (empty key "" for a non-field-specific error),
// matching ASP.NET Core ModelState conventions.
public sealed record ContractSaveResult(bool Success, ContractResponse? Contract, IReadOnlyDictionary<string, string[]> FieldErrors);
