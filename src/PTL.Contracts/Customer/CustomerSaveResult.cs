namespace PTL.Contracts.Customer;

// Returned by PTL.ApiClient's CreateCustomerAsync/UpdateCustomerAsync so callers can surface
// validation errors (HTTP 400) without needing to catch an HttpRequestException. FieldErrors is
// keyed by the CustomerFormViewModel property name (empty key "" for a non-field-specific error),
// matching ASP.NET Core ModelState conventions, so InternalWeb can attach each message to its
// exact field and render a GOV.UK error summary that links to it.
public sealed record CustomerSaveResult(bool Success, CustomerResponse? Customer, IReadOnlyDictionary<string, string[]> FieldErrors);
