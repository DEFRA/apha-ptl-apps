namespace PTL.Core.Contract;

// Thrown by ContractService when ContractValidator rejects a create/update; PTL.Api's
// ContractController translates this into a 400 ValidationProblem response.
public sealed class ContractValidationException(IReadOnlyList<ContractValidationError> errors) : Exception("Contract validation failed.")
{
    public IReadOnlyList<ContractValidationError> Errors { get; } = errors;
}
