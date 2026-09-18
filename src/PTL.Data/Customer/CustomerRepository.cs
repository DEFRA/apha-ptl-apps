using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PTL.Contracts.Customer;
using PTL.Core.Customer;
using CoreCustomer = PTL.Core.Customer.Customer;

namespace PTL.Data.Customer;

// Wraps the existing spgCustomerByCustomerId / spgaCustomerInfo stored procedures via EF Core;
// the database schema and procedure behaviour are owned elsewhere and are not modified here.
public sealed class CustomerRepository(PtlDbContext dbContext) : ICustomerRepository
{
    public async Task<CoreCustomer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        // EXEC ... is not composable SQL, so SingleOrDefaultAsync (which wraps the query) cannot be used here.
        var customerIdParameter = new SqlParameter("@CustomerId", customerId);

        var results = await dbContext.Customers
            .FromSqlRaw("EXEC dbo.spgCustomerByCustomerId @CustomerId", customerIdParameter)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return results.SingleOrDefault();
    }

    public async Task<IReadOnlyList<CustomerSummaryEntity>> GetSummariesAsync(CustomerStatusFilter status, CancellationToken cancellationToken = default)
    {
        var isActiveParameter = new SqlParameter("@IsActive", SqlDbType.Bit)
        {
            Value = status switch
            {
                CustomerStatusFilter.Active => true,
                CustomerStatusFilter.Inactive => false,
                _ => DBNull.Value
            }
        };

        return await dbContext.CustomerSummaries
            .FromSqlRaw("EXEC dbo.spgaCustomerInfo @IsActive", isActiveParameter)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<CoreCustomer> CreateAsync(CoreCustomer customer, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlRawAsync(InsertSql, BuildParameters(customer), cancellationToken);
        var created = await GetByIdAsync(customer.CustomerId, cancellationToken);
        return created ?? throw new InvalidOperationException($"Customer {customer.CustomerId} was inserted but could not be re-read.");
    }

    public async Task<CoreCustomer?> UpdateAsync(CoreCustomer customer, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await dbContext.Database.ExecuteSqlRawAsync(UpdateSql, BuildParameters(customer), cancellationToken);
        return rowsAffected == 0 ? null : await GetByIdAsync(customer.CustomerId, cancellationToken);
    }

    // Parameter order matches spiCustomer/spuCustomer exactly (see ProficiencyTestingDatabase/Object
    // Scripts/Stored Procedures/spi/spiCustomer.sql and spu/spuCustomer.sql). QalNumber is never
    // passed - spiCustomer generates it from a sequence, and spuCustomer never updates it.
    private const string InsertSql =
        "EXEC dbo.spiCustomer @CustomerId, @RegisteredFileNumber, @Name, @PreviousName, @CustomerTypeID, @VatNumber, @VatRatingId, @AccountNumber, @CustomerFinanceId, @ContactName, @Organisation, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @Telephone2, @Fax, @Email, @CurrencyId, @Comments, @PostageArrangements, @PaymentNonUK, @InvoiceName, @InvoiceOrganisation, @InvoiceAddress1, @InvoiceAddress2, @InvoiceAddress3, @InvoiceAddress4, @InvoiceAddress5, @InvoiceCountryId, @InvoiceTelephone, @InvoiceTelephone2, @InvoiceFax, @InvoiceEmail, @InitialStartDate, @IsActive, @CanOrderOnline, @InactiveDate, @CustomerStatusId";

    private const string UpdateSql =
        "EXEC dbo.spuCustomer @CustomerId, @RegisteredFileNumber, @Name, @PreviousName, @CustomerTypeID, @VatNumber, @VatRatingId, @AccountNumber, @CustomerFinanceId, @ContactName, @Organisation, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @Telephone2, @Fax, @Email, @CurrencyId, @Comments, @PostageArrangements, @PaymentNonUK, @InvoiceName, @InvoiceOrganisation, @InvoiceAddress1, @InvoiceAddress2, @InvoiceAddress3, @InvoiceAddress4, @InvoiceAddress5, @InvoiceCountryId, @InvoiceTelephone, @InvoiceTelephone2, @InvoiceFax, @InvoiceEmail, @InitialStartDate, @IsActive, @CanOrderOnline, @InactiveDate, @CustomerStatusId";

    private static SqlParameter[] BuildParameters(CoreCustomer customer) =>
    [
        new SqlParameter("@CustomerId", customer.CustomerId),
        new SqlParameter("@RegisteredFileNumber", customer.RegisteredFileNumber),
        new SqlParameter("@Name", customer.Name),
        new SqlParameter("@PreviousName", customer.PreviousName),
        new SqlParameter("@CustomerTypeID", customer.CustomerTypeId),
        new SqlParameter("@VatNumber", customer.VatNumber),
        new SqlParameter("@VatRatingId", customer.VatRatingId),
        new SqlParameter("@AccountNumber", customer.AccountNumber),
        new SqlParameter("@CustomerFinanceId", customer.CustomerFinanceId),
        new SqlParameter("@ContactName", customer.ContactName),
        new SqlParameter("@Organisation", customer.Organisation),
        new SqlParameter("@Address1", customer.Address1),
        new SqlParameter("@Address2", customer.Address2),
        new SqlParameter("@Address3", customer.Address3),
        new SqlParameter("@Address4", customer.Address4),
        new SqlParameter("@Address5", customer.Address5),
        new SqlParameter("@CountryId", customer.CountryId),
        new SqlParameter("@Telephone", customer.Telephone),
        new SqlParameter("@Telephone2", customer.Telephone2),
        new SqlParameter("@Fax", customer.Fax),
        new SqlParameter("@Email", customer.Email),
        new SqlParameter("@CurrencyId", customer.CurrencyId),
        new SqlParameter("@Comments", customer.Comments),
        new SqlParameter("@PostageArrangements", customer.PostageArrangements),
        new SqlParameter("@PaymentNonUK", customer.PaymentNonUK),
        new SqlParameter("@InvoiceName", customer.InvoiceName),
        new SqlParameter("@InvoiceOrganisation", customer.InvoiceOrganisation),
        new SqlParameter("@InvoiceAddress1", customer.InvoiceAddress1),
        new SqlParameter("@InvoiceAddress2", customer.InvoiceAddress2),
        new SqlParameter("@InvoiceAddress3", customer.InvoiceAddress3),
        new SqlParameter("@InvoiceAddress4", customer.InvoiceAddress4),
        new SqlParameter("@InvoiceAddress5", customer.InvoiceAddress5),
        new SqlParameter("@InvoiceCountryId", customer.InvoiceCountryId),
        new SqlParameter("@InvoiceTelephone", customer.InvoiceTelephone),
        new SqlParameter("@InvoiceTelephone2", customer.InvoiceTelephone2),
        new SqlParameter("@InvoiceFax", customer.InvoiceFax),
        new SqlParameter("@InvoiceEmail", customer.InvoiceEmail),
        new SqlParameter("@InitialStartDate", customer.InitialStartDate),
        new SqlParameter("@IsActive", customer.IsActive),
        new SqlParameter("@CanOrderOnline", customer.CanOrderOnline),
        new SqlParameter("@InactiveDate", (object?)customer.InactiveDate ?? DBNull.Value),
        new SqlParameter("@CustomerStatusId", (object?)customer.CustomerStatusId ?? DBNull.Value)
    ];
}
