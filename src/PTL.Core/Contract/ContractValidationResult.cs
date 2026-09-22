namespace PTL.Core.Contract;

// Field is the ContractFormViewModel/CreateContractRequest property name the error belongs to
// (empty string for a model-level error with no single field), so callers can render a GOV.UK
// error summary that links each message to its exact field.
public sealed record ContractValidationError(string Field, string Message);

public sealed record ContractValidationResult(bool IsValid, IReadOnlyList<ContractValidationError> Errors);
