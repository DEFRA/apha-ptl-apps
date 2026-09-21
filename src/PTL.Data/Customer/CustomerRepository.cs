using System.Data;
using Dapper;
using PTL.Contracts.Customer;
using PTL.Core.Customer;
using PTL.Data.Infrastructure;
using CoreCustomer = PTL.Core.Customer.Customer;

namespace PTL.Data.Customer;

// The database schema and stored procedure contracts are owned elsewhere. This repository executes
// the legacy SQL via Dapper against a connection obtained from IDbConnectionFactory.
public sealed class CustomerRepository(IDbConnectionFactory connectionFactory) : ICustomerRepository
{
    public async Task<CoreCustomer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<CoreCustomer>(
            "EXEC dbo.spgCustomerByCustomerId @CustomerId",
            new { CustomerId = customerId });
    }

    public async Task<IReadOnlyList<CustomerSummaryEntity>> GetSummariesAsync(CustomerStatusFilter status, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        var isActive = status switch
        {
            CustomerStatusFilter.Active => true,
            CustomerStatusFilter.Inactive => false,
            _ => (bool?)null
        };

        return (await connection.QueryAsync<CustomerSummaryEntity>(
            "EXEC dbo.spgaCustomerInfo @IsActive",
            new { IsActive = isActive })).ToList();
    }

    public async Task<CoreCustomer> CreateAsync(CoreCustomer customer, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(InsertSql, BuildParameters(customer));
        var created = await GetByIdAsync(customer.CustomerId, cancellationToken);
        return created ?? throw new InvalidOperationException($"Customer {customer.CustomerId} was inserted but could not be re-read.");
    }

    public async Task<CoreCustomer?> UpdateAsync(CoreCustomer customer, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        var rowsAffected = await connection.ExecuteAsync(UpdateSql, BuildParameters(customer));
        return rowsAffected == 0 ? null : await GetByIdAsync(customer.CustomerId, cancellationToken);
    }

    private const string InsertSql =
        "EXEC dbo.spiCustomer @CustomerId, @RegisteredFileNumber, @Name, @PreviousName, @CustomerTypeID, @VatNumber, @VatRatingId, @AccountNumber, @CustomerFinanceId, @ContactName, @Organisation, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @Telephone2, @Fax, @Email, @CurrencyId, @Comments, @PostageArrangements, @PaymentNonUK, @InvoiceName, @InvoiceOrganisation, @InvoiceAddress1, @InvoiceAddress2, @InvoiceAddress3, @InvoiceAddress4, @InvoiceAddress5, @InvoiceCountryId, @InvoiceTelephone, @InvoiceTelephone2, @InvoiceFax, @InvoiceEmail, @InitialStartDate, @IsActive, @CanOrderOnline, @InactiveDate, @CustomerStatusId";

    private const string UpdateSql =
        "EXEC dbo.spuCustomer @CustomerId, @RegisteredFileNumber, @Name, @PreviousName, @CustomerTypeID, @VatNumber, @VatRatingId, @AccountNumber, @CustomerFinanceId, @ContactName, @Organisation, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @Telephone2, @Fax, @Email, @CurrencyId, @Comments, @PostageArrangements, @PaymentNonUK, @InvoiceName, @InvoiceOrganisation, @InvoiceAddress1, @InvoiceAddress2, @InvoiceAddress3, @InvoiceAddress4, @InvoiceAddress5, @InvoiceCountryId, @InvoiceTelephone, @InvoiceTelephone2, @InvoiceFax, @InvoiceEmail, @InitialStartDate, @IsActive, @CanOrderOnline, @InactiveDate, @CustomerStatusId";

    private static DynamicParameters BuildParameters(CoreCustomer customer)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@CustomerId", customer.CustomerId);
        parameters.Add("@RegisteredFileNumber", customer.RegisteredFileNumber);
        parameters.Add("@Name", customer.Name);
        parameters.Add("@PreviousName", customer.PreviousName);
        parameters.Add("@CustomerTypeID", customer.CustomerTypeId);
        parameters.Add("@VatNumber", customer.VatNumber);
        parameters.Add("@VatRatingId", customer.VatRatingId);
        parameters.Add("@AccountNumber", customer.AccountNumber);
        parameters.Add("@CustomerFinanceId", customer.CustomerFinanceId);
        parameters.Add("@ContactName", customer.ContactName);
        parameters.Add("@Organisation", customer.Organisation);
        parameters.Add("@Address1", customer.Address1);
        parameters.Add("@Address2", customer.Address2);
        parameters.Add("@Address3", customer.Address3);
        parameters.Add("@Address4", customer.Address4);
        parameters.Add("@Address5", customer.Address5);
        parameters.Add("@CountryId", customer.CountryId);
        parameters.Add("@Telephone", customer.Telephone);
        parameters.Add("@Telephone2", customer.Telephone2);
        parameters.Add("@Fax", customer.Fax);
        parameters.Add("@Email", customer.Email);
        parameters.Add("@CurrencyId", customer.CurrencyId);
        parameters.Add("@Comments", customer.Comments);
        parameters.Add("@PostageArrangements", customer.PostageArrangements);
        parameters.Add("@PaymentNonUK", customer.PaymentNonUK);
        parameters.Add("@InvoiceName", customer.InvoiceName);
        parameters.Add("@InvoiceOrganisation", customer.InvoiceOrganisation);
        parameters.Add("@InvoiceAddress1", customer.InvoiceAddress1);
        parameters.Add("@InvoiceAddress2", customer.InvoiceAddress2);
        parameters.Add("@InvoiceAddress3", customer.InvoiceAddress3);
        parameters.Add("@InvoiceAddress4", customer.InvoiceAddress4);
        parameters.Add("@InvoiceAddress5", customer.InvoiceAddress5);
        parameters.Add("@InvoiceCountryId", customer.InvoiceCountryId);
        parameters.Add("@InvoiceTelephone", customer.InvoiceTelephone);
        parameters.Add("@InvoiceTelephone2", customer.InvoiceTelephone2);
        parameters.Add("@InvoiceFax", customer.InvoiceFax);
        parameters.Add("@InvoiceEmail", customer.InvoiceEmail);
        parameters.Add("@InitialStartDate", customer.InitialStartDate);
        parameters.Add("@IsActive", customer.IsActive);
        parameters.Add("@CanOrderOnline", customer.CanOrderOnline);
        parameters.Add("@InactiveDate", customer.InactiveDate);
        parameters.Add("@CustomerStatusId", customer.CustomerStatusId);
        return parameters;
    }
}
