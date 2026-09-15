using Microsoft.Extensions.Logging;
using PTL.Contracts.Customer;

namespace PTL.Core.Customer;

public sealed class CustomerService(ICustomerRepository customerRepository, ILogger<CustomerService> logger) : ICustomerService
{
    public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        customerRepository.GetByIdAsync(customerId, cancellationToken);

    public Task<IReadOnlyList<CustomerSummaryEntity>> GetCustomersAsync(CustomerStatusFilter status, CancellationToken cancellationToken = default) =>
        customerRepository.GetSummariesAsync(status, cancellationToken);

    public async Task<CustomerSearchResult> SearchCustomersAsync(string? searchTerm, CustomerStatusFilter status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var all = await customerRepository.GetSummariesAsync(status, cancellationToken);

        var filtered = string.IsNullOrWhiteSpace(searchTerm)
            ? all
            : all.Where(c =>
                c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.QalNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.Organisation.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var totalCount = filtered.Count;
        var page1 = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        logger.LogInformation(
            "Customer search: searchTerm={SearchTerm} status={Status} page={Page} pageSize={PageSize} totalResults={TotalCount}",
            searchTerm, status, page, pageSize, totalCount);

        return new CustomerSearchResult(page1, totalCount);
    }

    public async Task<Customer> CreateCustomerAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        // CustomerId and InitialStartDate are always server-generated, matching the legacy
        // Customer.DataPortal_Create() behaviour - never trust client-supplied values here.
        customer.CustomerId = Guid.NewGuid();
        customer.InitialStartDate = DateTime.UtcNow;
        ApplyStatusTransition(customer);

        Validate(customer);

        var created = await customerRepository.CreateAsync(customer, cancellationToken);
        logger.LogInformation("Created customer {CustomerId} ({QalNumber})", created.CustomerId, created.QalNumber);
        return created;
    }

    public async Task<Customer?> UpdateCustomerAsync(Guid customerId, Customer updatedFields, CancellationToken cancellationToken = default)
    {
        var existing = await customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (existing is null)
        {
            logger.LogWarning("Update requested for unknown customer {CustomerId}", customerId);
            return null;
        }

        // QalNumber and InitialStartDate are immutable after creation, matching the legacy UI
        // (Customer.aspx never posts these fields back).
        updatedFields.CustomerId = existing.CustomerId;
        updatedFields.QalNumber = existing.QalNumber;
        updatedFields.InitialStartDate = existing.InitialStartDate;
        ApplyStatusTransition(updatedFields);

        Validate(updatedFields);

        var updated = await customerRepository.UpdateAsync(updatedFields, cancellationToken);
        logger.LogInformation("Updated customer {CustomerId} ({QalNumber})", customerId, updatedFields.QalNumber);
        return updated;
    }

    public async Task<Customer?> DeactivateCustomerAsync(Guid customerId, Guid customerStatusId, CancellationToken cancellationToken = default)
    {
        var existing = await customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (existing is null)
        {
            logger.LogWarning("Deactivate requested for unknown customer {CustomerId}", customerId);
            return null;
        }

        existing.IsActive = false;
        existing.CustomerStatusId = customerStatusId;
        ApplyStatusTransition(existing);

        Validate(existing);

        var updated = await customerRepository.UpdateAsync(existing, cancellationToken);
        logger.LogInformation("Deactivated customer {CustomerId} ({QalNumber}) with status {CustomerStatusId}", customerId, existing.QalNumber, customerStatusId);
        return updated;
    }

    public async Task<Customer?> ReactivateCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var existing = await customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (existing is null)
        {
            logger.LogWarning("Reactivate requested for unknown customer {CustomerId}", customerId);
            return null;
        }

        existing.IsActive = true;
        ApplyStatusTransition(existing);

        Validate(existing);

        var updated = await customerRepository.UpdateAsync(existing, cancellationToken);
        logger.LogInformation("Reactivated customer {CustomerId} ({QalNumber})", customerId, existing.QalNumber);
        return updated;
    }

    // Mirrors Customer.aspx.vb's LoadObjectFromForm: activating clears the inactive markers;
    // deactivating stamps InactiveDate only if one is not already set. Does not cascade to
    // participants/viewers - that is explicitly out of scope for the Customer domain (see
    // customer-migration.md; participant integration is a separate task).
    private static void ApplyStatusTransition(Customer customer)
    {
        if (customer.IsActive)
        {
            customer.InactiveDate = null;
            customer.CustomerStatusId = null;
        }
        else if (customer.InactiveDate is null)
        {
            customer.InactiveDate = DateTime.UtcNow;
        }
    }

    private void Validate(Customer customer)
    {
        var result = CustomerValidator.Validate(customer);
        if (!result.IsValid)
        {
            logger.LogWarning("Customer validation failed for {CustomerId}: {Errors}", customer.CustomerId, string.Join("; ", result.Errors.Select(e => e.Message)));
            throw new CustomerValidationException(result.Errors);
        }
    }
}
