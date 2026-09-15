using PTL.Contracts.Customer;
using PTL.Core.Customer;

namespace PTL.Api.Tests.Customer;

// In-memory ICustomerRepository test double so CustomerService can be tested without a real
// database or the spgCustomer*/spiCustomer/spuCustomer stored procedures.
internal sealed class FakeCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<Guid, PTL.Core.Customer.Customer> _customers = new();

    public IReadOnlyDictionary<Guid, PTL.Core.Customer.Customer> Customers => _customers;

    public Task<PTL.Core.Customer.Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_customers.TryGetValue(customerId, out var customer) ? Clone(customer) : null);

    public Task<IReadOnlyList<CustomerSummaryEntity>> GetSummariesAsync(CustomerStatusFilter status, CancellationToken cancellationToken = default)
    {
        var filtered = _customers.Values.Where(c => status switch
        {
            CustomerStatusFilter.Active => c.IsActive,
            CustomerStatusFilter.Inactive => !c.IsActive,
            _ => true
        });

        IReadOnlyList<CustomerSummaryEntity> summaries = filtered
            .Select(c => new CustomerSummaryEntity { CustomerId = c.CustomerId, QalNumber = c.QalNumber, Name = c.Name, Organisation = c.Organisation, IsActive = c.IsActive })
            .ToList();

        return Task.FromResult(summaries);
    }

    public Task<PTL.Core.Customer.Customer> CreateAsync(PTL.Core.Customer.Customer customer, CancellationToken cancellationToken = default)
    {
        customer.QalNumber = $"QAL/{_customers.Count + 1:00000}";
        _customers[customer.CustomerId] = Clone(customer);
        return Task.FromResult(Clone(customer));
    }

    public Task<PTL.Core.Customer.Customer?> UpdateAsync(PTL.Core.Customer.Customer customer, CancellationToken cancellationToken = default)
    {
        if (!_customers.ContainsKey(customer.CustomerId))
        {
            return Task.FromResult<PTL.Core.Customer.Customer?>(null);
        }

        _customers[customer.CustomerId] = Clone(customer);
        return Task.FromResult<PTL.Core.Customer.Customer?>(Clone(customer));
    }

    private static PTL.Core.Customer.Customer Clone(PTL.Core.Customer.Customer source) => new()
    {
        CustomerId = source.CustomerId,
        QalNumber = source.QalNumber,
        RegisteredFileNumber = source.RegisteredFileNumber,
        Name = source.Name,
        PreviousName = source.PreviousName,
        CustomerTypeId = source.CustomerTypeId,
        VatNumber = source.VatNumber,
        VatRatingId = source.VatRatingId,
        AccountNumber = source.AccountNumber,
        CustomerFinanceId = source.CustomerFinanceId,
        ContactName = source.ContactName,
        Organisation = source.Organisation,
        Address1 = source.Address1,
        Address2 = source.Address2,
        Address3 = source.Address3,
        Address4 = source.Address4,
        Address5 = source.Address5,
        CountryId = source.CountryId,
        Telephone = source.Telephone,
        Telephone2 = source.Telephone2,
        Fax = source.Fax,
        Email = source.Email,
        CurrencyId = source.CurrencyId,
        Comments = source.Comments,
        InitialStartDate = source.InitialStartDate,
        PostageArrangements = source.PostageArrangements,
        PaymentNonUK = source.PaymentNonUK,
        InvoiceName = source.InvoiceName,
        InvoiceOrganisation = source.InvoiceOrganisation,
        InvoiceAddress1 = source.InvoiceAddress1,
        InvoiceAddress2 = source.InvoiceAddress2,
        InvoiceAddress3 = source.InvoiceAddress3,
        InvoiceAddress4 = source.InvoiceAddress4,
        InvoiceAddress5 = source.InvoiceAddress5,
        InvoiceCountryId = source.InvoiceCountryId,
        InvoiceTelephone = source.InvoiceTelephone,
        InvoiceTelephone2 = source.InvoiceTelephone2,
        InvoiceFax = source.InvoiceFax,
        InvoiceEmail = source.InvoiceEmail,
        IsActive = source.IsActive,
        CanOrderOnline = source.CanOrderOnline,
        InactiveDate = source.InactiveDate,
        CustomerStatusId = source.CustomerStatusId
    };
}
