using PTL.Contracts.Customer;

namespace PTL.Core.Customer;

public interface ICustomerService
{
    Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerSummaryEntity>> GetCustomersAsync(CustomerStatusFilter status, CancellationToken cancellationToken = default);

    // In-memory search/filter/paging over GetCustomersAsync's result set - see CustomerService for rationale.
    Task<CustomerSearchResult> SearchCustomersAsync(string? searchTerm, CustomerStatusFilter status, int page, int pageSize, CancellationToken cancellationToken = default);

    // Throws CustomerValidationException when business rules are violated.
    Task<Customer> CreateCustomerAsync(Customer customer, CancellationToken cancellationToken = default);

    // Returns null when customerId does not exist. Throws CustomerValidationException when business rules are violated.
    Task<Customer?> UpdateCustomerAsync(Guid customerId, Customer updatedFields, CancellationToken cancellationToken = default);
}
