namespace PTL.Core.Customer;

// Thrown by CustomerService when CustomerValidator rejects a create/update; PTL.Api's
// CustomerController translates this into a 400 ValidationProblem response.
public sealed class CustomerValidationException(IReadOnlyList<CustomerValidationError> errors) : Exception("Customer validation failed.")
{
    public IReadOnlyList<CustomerValidationError> Errors { get; } = errors;
}
