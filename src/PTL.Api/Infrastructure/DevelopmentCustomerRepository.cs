using PTL.Contracts.Customer;
using PTL.Core.Customer;
using PTL.Data.Customer;

namespace PTL.Api.Infrastructure;

// TEMPORARY (Development only): the local dev database is empty or unreachable, so the
// Customer screens would otherwise render empty or throw. This decorator falls back to in-memory
// sample data whenever the real query returns nothing OR throws (e.g. SQL Server not running).
// Safe to delete this file and its single registration in Program.cs at any time - CustomerRepository,
// CustomerService, the controllers and views are completely unaffected.
internal sealed class DevelopmentCustomerRepository(CustomerRepository inner) : ICustomerRepository
{
    private static readonly IReadOnlyList<CustomerSummaryEntity> DummySummaries =
    [
        new() { CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111"), QalNumber = "QAL/00001", Name = "Sample Laboratories Ltd", Organisation = "Sample Laboratories Ltd", IsActive = true },
        new() { CustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222"), QalNumber = "QAL/00002", Name = "Northfield Veterinary Practice", Organisation = "Northfield Veterinary Practice", IsActive = true },
        new() { CustomerId = Guid.Parse("33333333-3333-3333-3333-333333333333"), QalNumber = "QAL/00003", Name = "Old Mill Research Institute", Organisation = "Old Mill Research Institute", IsActive = false }
    ];

    private static readonly IReadOnlyDictionary<Guid, Customer> DummyCustomers = DummySummaries.ToDictionary(
        s => s.CustomerId,
        s => new Customer
        {
            CustomerId = s.CustomerId,
            QalNumber = s.QalNumber,
            RegisteredFileNumber = s.QalNumber.Replace("QAL/", string.Empty),
            Name = s.Name,
            Organisation = s.Organisation,
            ContactName = "Sample Contact",
            Address1 = "1 Sample Street",
            Address2 = "Sample District",
            Telephone = "01234 567890",
            Email = "sample.contact@example.com",
            InvoiceName = "Sample Contact",
            InvoiceOrganisation = s.Organisation,
            InvoiceAddress1 = "1 Sample Street",
            InvoiceAddress2 = "Sample District",
            InvoiceTelephone = "01234 567890",
            InvoiceEmail = "sample.invoicing@example.com",
            InitialStartDate = new DateTime(2024, 1, 1),
            IsActive = s.IsActive,
            CanOrderOnline = s.IsActive,
            InactiveDate = s.IsActive ? null : new DateTime(2025, 6, 1)
        });

    public async Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        Customer? customer;
        try
        {
            customer = await inner.GetByIdAsync(customerId, cancellationToken);
        }
        catch (Exception)
        {
            customer = null;
        }

        return customer ?? DummyCustomers.GetValueOrDefault(customerId);
    }

    public async Task<IReadOnlyList<CustomerSummaryEntity>> GetSummariesAsync(CustomerStatusFilter status, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CustomerSummaryEntity> customers;
        try
        {
            customers = await inner.GetSummariesAsync(status, cancellationToken);
        }
        catch (Exception)
        {
            customers = [];
        }

        if (customers.Count > 0)
        {
            return customers;
        }

        return status switch
        {
            CustomerStatusFilter.Active => DummySummaries.Where(c => c.IsActive).ToList(),
            CustomerStatusFilter.Inactive => DummySummaries.Where(c => !c.IsActive).ToList(),
            _ => DummySummaries
        };
    }

    // Create/Update always go straight to the real database - faking a write would be misleading.
    public Task<Customer> CreateAsync(Customer customer, CancellationToken cancellationToken = default) =>
        inner.CreateAsync(customer, cancellationToken);

    public Task<Customer?> UpdateAsync(Customer customer, CancellationToken cancellationToken = default) =>
        inner.UpdateAsync(customer, cancellationToken);
}
