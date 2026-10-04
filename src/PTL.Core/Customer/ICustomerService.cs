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

    // spgaPendingCustomerDetailsEditInfo - every outstanding pending customer update.
    Task<IReadOnlyList<PendingCustomerUpdateSummaryEntity>> GetPendingCustomerUpdatesAsync(CancellationToken cancellationToken = default);

    // Returns null when the customer or its pending update does not exist.
    Task<(Customer Current, PendingCustomerUpdate Pending)?> GetPendingCustomerUpdateAsync(Guid customerId, CancellationToken cancellationToken = default);

    // Applies the pending contact/invoice-contact fields onto the live customer record, then
    // soft-deletes the pending update. When editedFields is supplied those values are applied in
    // place of the stored pending values (legacy ButtonApprove_Click writes the on-screen values to
    // both records). Returns false when no matching pending update exists. Throws
    // CustomerValidationException when the resulting customer breaks business rules.
    Task<bool> ApprovePendingCustomerUpdateAsync(Guid customerId, PendingCustomerUpdate? editedFields = null, CancellationToken cancellationToken = default);

    // Soft-deletes the pending update without changing the live customer record. Returns false
    // when no matching pending update exists.
    Task<bool> DeclinePendingCustomerUpdateAsync(Guid customerId, CancellationToken cancellationToken = default);
}
