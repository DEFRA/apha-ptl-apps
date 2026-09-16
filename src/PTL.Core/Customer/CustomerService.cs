using Microsoft.Extensions.Logging;
using PTL.Contracts.Customer;

namespace PTL.Core.Customer;

public sealed class CustomerService(ICustomerRepository customerRepository, ILogger<CustomerService> logger) : ICustomerService
{
    private static readonly Action<ILogger, string?, CustomerStatusFilter, int, int, int, Exception?> LogCustomerSearchMessage =
        LoggerMessage.Define<string?, CustomerStatusFilter, int, int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogCustomerSearchMessage)),
            "Customer search: searchTerm={SearchTerm} status={Status} page={Page} pageSize={PageSize} totalResults={TotalCount}");

    private static readonly Action<ILogger, Guid, string, Exception?> LogCreatedCustomerMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            new EventId(2, nameof(LogCreatedCustomerMessage)),
            "Created customer {CustomerId} ({QalNumber})");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdateUnknownCustomerMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(3, nameof(LogUpdateUnknownCustomerMessage)),
            "Update requested for unknown customer {CustomerId}");

    private static readonly Action<ILogger, Guid, string, Exception?> LogUpdatedCustomerMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            new EventId(4, nameof(LogUpdatedCustomerMessage)),
            "Updated customer {CustomerId} ({QalNumber})");

    private static readonly Action<ILogger, Guid, Exception?> LogDeactivateUnknownCustomerMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(5, nameof(LogDeactivateUnknownCustomerMessage)),
            "Deactivate requested for unknown customer {CustomerId}");

    private static readonly Action<ILogger, Guid, string, Guid, Exception?> LogDeactivatedCustomerMessage =
        LoggerMessage.Define<Guid, string, Guid>(
            LogLevel.Information,
            new EventId(6, nameof(LogDeactivatedCustomerMessage)),
            "Deactivated customer {CustomerId} ({QalNumber}) with status {CustomerStatusId}");

    private static readonly Action<ILogger, Guid, Exception?> LogReactivateUnknownCustomerMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(7, nameof(LogReactivateUnknownCustomerMessage)),
            "Reactivate requested for unknown customer {CustomerId}");

    private static readonly Action<ILogger, Guid, string, Exception?> LogReactivatedCustomerMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            new EventId(8, nameof(LogReactivatedCustomerMessage)),
            "Reactivated customer {CustomerId} ({QalNumber})");

    private static readonly Action<ILogger, Guid, string, Exception?> LogCustomerValidationFailedMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(9, nameof(LogCustomerValidationFailedMessage)),
            "Customer validation failed for {CustomerId}: {Errors}");

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

        LogCustomerSearchMessage(logger, searchTerm, status, page, pageSize, totalCount, null);

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
        LogCreatedCustomerMessage(logger, created.CustomerId, created.QalNumber, null);
        return created;
    }

    public async Task<Customer?> UpdateCustomerAsync(Guid customerId, Customer updatedFields, CancellationToken cancellationToken = default)
    {
        var existing = await customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (existing is null)
        {
            LogUpdateUnknownCustomerMessage(logger, customerId, null);
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
        LogUpdatedCustomerMessage(logger, customerId, updatedFields.QalNumber, null);
        return updated;
    }

    public async Task<Customer?> DeactivateCustomerAsync(Guid customerId, Guid customerStatusId, CancellationToken cancellationToken = default)
    {
        var existing = await customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (existing is null)
        {
            LogDeactivateUnknownCustomerMessage(logger, customerId, null);
            return null;
        }

        existing.IsActive = false;
        existing.CustomerStatusId = customerStatusId;
        ApplyStatusTransition(existing);

        Validate(existing);

        var updated = await customerRepository.UpdateAsync(existing, cancellationToken);
        LogDeactivatedCustomerMessage(logger, customerId, existing.QalNumber, customerStatusId, null);
        return updated;
    }

    public async Task<Customer?> ReactivateCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var existing = await customerRepository.GetByIdAsync(customerId, cancellationToken);
        if (existing is null)
        {
            LogReactivateUnknownCustomerMessage(logger, customerId, null);
            return null;
        }

        existing.IsActive = true;
        ApplyStatusTransition(existing);

        Validate(existing);

        var updated = await customerRepository.UpdateAsync(existing, cancellationToken);
        LogReactivatedCustomerMessage(logger, customerId, existing.QalNumber, null);
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
            LogCustomerValidationFailedMessage(logger, customer.CustomerId, string.Join("; ", result.Errors.Select(e => e.Message)), null);
            throw new CustomerValidationException(result.Errors);
        }
    }
}
