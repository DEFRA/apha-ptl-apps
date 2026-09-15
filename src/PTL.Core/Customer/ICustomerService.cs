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

    // Sets IsActive=false, stamps InactiveDate, and records the chosen inactive-reason status -
    // mirrors Customer.aspx.vb's ButtonSave_Click status-change branch (participant/viewer
    // cascade is explicitly out of scope for the Customer domain). Returns null when
    // customerId does not exist. Throws CustomerValidationException when business rules are violated.
    Task<Customer?> DeactivateCustomerAsync(Guid customerId, Guid customerStatusId, CancellationToken cancellationToken = default);

    // Sets IsActive=true and clears InactiveDate/CustomerStatusId. Returns null when customerId
    // does not exist. Throws CustomerValidationException when business rules are violated.
    Task<Customer?> ReactivateCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}
