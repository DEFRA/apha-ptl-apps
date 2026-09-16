namespace PTL.Core.Customer;

// Field is the CustomerFormViewModel/CreateCustomerRequest property name the error belongs to
// (empty string for a model-level error with no single field), so callers can render a GOV.UK
// error summary that links each message to its exact field.
public sealed record CustomerValidationError(string Field, string Message);

public sealed record CustomerValidationResult(bool IsValid, IReadOnlyList<CustomerValidationError> Errors);
