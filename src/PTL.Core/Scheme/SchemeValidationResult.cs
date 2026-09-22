namespace PTL.Core.Scheme;

// Field is the SchemeFormViewModel/CreateSchemeRequest property name the error belongs to
// (empty string for a model-level error with no single field) - see ContractValidationError.
public sealed record SchemeValidationError(string Field, string Message);

public sealed record SchemeValidationResult(bool IsValid, IReadOnlyList<SchemeValidationError> Errors);
