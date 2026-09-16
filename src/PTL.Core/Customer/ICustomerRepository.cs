using PTL.Contracts.Customer;

namespace PTL.Core.Customer;

// Defined in Core (not Data) so CustomerService can depend on the abstraction without Core
// referencing Data; PTL.Data.Customer.CustomerRepository implements this.
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerSummaryEntity>> GetSummariesAsync(CustomerStatusFilter status, CancellationToken cancellationToken = default);

    // Uses spiCustomer; the returned Customer is re-read via GetByIdAsync so QalNumber (generated
    // by the stored procedure's sequence) is populated.
    Task<Customer> CreateAsync(Customer customer, CancellationToken cancellationToken = default);

    // Uses spuCustomer; returns null when no row was updated (customer does not exist).
    Task<Customer?> UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
}
