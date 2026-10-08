using System.Data;
using PTL.Contracts.Customer;
using PTL.Core.Customer;
using PTL.Data.Customer;
using PTL.Data.Infrastructure;
using PTL.Data.Tests.Fakes;
using CoreCustomer = PTL.Core.Customer.Customer;

namespace PTL.Data.Tests.Customer;

// Exercises CustomerRepository against a fake ADO.NET connection (see Fakes/) instead of a live
// SQL Server - see ContractRepositoryTests for the pattern this follows.
public class CustomerRepositoryTests
{
    public CustomerRepositoryTests() => DapperColumnMappings.Register();

    private const string GetByIdSql = "EXEC dbo.spgCustomerByCustomerId @CustomerId";

    // Must stay byte-identical to CustomerRepository.SummariesSql - the fake connection matches on text.
    private const string GetSummariesSql =
        """
        SELECT
            c.fldCustomerId,
            c.fldQalNumber,
            c.fldName,
            c.fldOrganisation,
            ISNULL(c.fldContactName, '') AS fldContactName,
            ISNULL(c.fldAccountNumber, '') AS fldAccountNumber,
            ISNULL(ct.fldCountry, '') AS fldCountry,
            c.fldIsActive
        FROM dbo.tblCustomer c
        LEFT JOIN dbo.tblCountry ct ON c.fldCountryId = ct.fldCountryId
        WHERE (@IsActive IS NULL OR c.fldIsActive = @IsActive)
        ORDER BY
            c.fldIsActive DESC,
            CONVERT(int, SUBSTRING(c.fldQalNumber, 5, LEN(c.fldQalNumber)))
        """;
    private const string InsertSql =
        "EXEC dbo.spiCustomer @CustomerId, @RegisteredFileNumber, @Name, @PreviousName, @CustomerTypeID, @VatNumber, @VatRatingId, @AccountNumber, @CustomerFinanceId, @ContactName, @Organisation, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @Telephone2, @Fax, @Email, @CurrencyId, @Comments, @PostageArrangements, @PaymentNonUK, @InvoiceName, @InvoiceOrganisation, @InvoiceAddress1, @InvoiceAddress2, @InvoiceAddress3, @InvoiceAddress4, @InvoiceAddress5, @InvoiceCountryId, @InvoiceTelephone, @InvoiceTelephone2, @InvoiceFax, @InvoiceEmail, @InitialStartDate, @IsActive, @CanOrderOnline, @InactiveDate, @CustomerStatusId";
    private const string UpdateSql =
        "EXEC dbo.spuCustomer @CustomerId, @RegisteredFileNumber, @Name, @PreviousName, @CustomerTypeID, @VatNumber, @VatRatingId, @AccountNumber, @CustomerFinanceId, @ContactName, @Organisation, @Address1, @Address2, @Address3, @Address4, @Address5, @CountryId, @Telephone, @Telephone2, @Fax, @Email, @CurrencyId, @Comments, @PostageArrangements, @PaymentNonUK, @InvoiceName, @InvoiceOrganisation, @InvoiceAddress1, @InvoiceAddress2, @InvoiceAddress3, @InvoiceAddress4, @InvoiceAddress5, @InvoiceCountryId, @InvoiceTelephone, @InvoiceTelephone2, @InvoiceFax, @InvoiceEmail, @InitialStartDate, @IsActive, @CanOrderOnline, @InactiveDate, @CustomerStatusId";

    private static DataTable CustomerTable(Guid customerId, bool isActive = true)
    {
        var table = new DataTable();
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldQalNumber", typeof(string));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldOrganisation", typeof(string));
        table.Columns.Add("fldIsActive", typeof(bool));
        table.Rows.Add(customerId, "QAL0001", "Test Customer", "Test Org", isActive);
        return table;
    }

    private static DataTable SummaryTable(Guid customerId, bool isActive = true)
    {
        var table = new DataTable();
        table.Columns.Add("fldCustomerId", typeof(Guid));
        table.Columns.Add("fldQalNumber", typeof(string));
        table.Columns.Add("fldName", typeof(string));
        table.Columns.Add("fldOrganisation", typeof(string));
        table.Columns.Add("fldContactName", typeof(string));
        table.Columns.Add("fldAccountNumber", typeof(string));
        table.Columns.Add("fldCountry", typeof(string));
        table.Columns.Add("fldIsActive", typeof(bool));
        table.Rows.Add(customerId, "QAL0001", "Test Customer", "Test Org", "Alice Example", "ACC-12345", "United Kingdom", isActive);
        return table;
    }

    private static (CustomerRepository Repository, FakeDbConnection Connection) CreateRepository()
    {
        var connection = new FakeDbConnection();
        var repository = new CustomerRepository(new FakeDbConnectionFactory(connection));
        return (repository, connection);
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedCustomer()
    {
        var (repository, connection) = CreateRepository();
        var customerId = Guid.NewGuid();
        connection.RespondToQuery(GetByIdSql, CustomerTable(customerId));

        var result = await repository.GetByIdAsync(customerId);

        Assert.NotNull(result);
        Assert.Equal(customerId, result!.CustomerId);
        Assert.Equal("Test Customer", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetByIdSql, new DataTable());

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSummariesAsync_ReturnsMappedSummaries()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetSummariesSql, SummaryTable(Guid.NewGuid()));

        var result = await repository.GetSummariesAsync(CustomerStatusFilter.Active);

        Assert.Single(result);
        Assert.Equal("Test Customer", result[0].Name);
        Assert.Equal("Alice Example", result[0].ContactName);
        Assert.Equal("ACC-12345", result[0].AccountNumber);
        Assert.Equal("United Kingdom", result[0].Country);
        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == GetSummariesSql);
        Assert.Equal(true, command.ParameterValue("@IsActive"));
    }

    [Fact]
    public async Task GetSummariesAsync_Inactive_PassesFalseIsActive()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetSummariesSql, SummaryTable(Guid.NewGuid(), isActive: false));

        await repository.GetSummariesAsync(CustomerStatusFilter.Inactive);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == GetSummariesSql);
        Assert.Equal(false, command.ParameterValue("@IsActive"));
    }

    [Fact]
    public async Task GetSummariesAsync_All_PassesNullIsActive()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToQuery(GetSummariesSql, SummaryTable(Guid.NewGuid()));

        await repository.GetSummariesAsync(CustomerStatusFilter.All);

        var command = Assert.Single(connection.ExecutedCommands, c => c.CommandText == GetSummariesSql);
        Assert.Equal(DBNull.Value, command.ParameterValue("@IsActive"));
    }

    [Fact]
    public async Task CreateAsync_InsertsAndReReadsCustomer()
    {
        var (repository, connection) = CreateRepository();
        var customerId = Guid.NewGuid();
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(GetByIdSql, CustomerTable(customerId));
        var customer = new CoreCustomer { CustomerId = customerId, Name = "Test Customer", IsActive = true };

        var result = await repository.CreateAsync(customer);

        Assert.Equal(customerId, result.CustomerId);
        var insertCommand = Assert.Single(connection.ExecutedCommands, c => c.CommandText == InsertSql);
        Assert.Equal(customerId, insertCommand.ParameterValue("@CustomerId"));
    }

    [Fact]
    public async Task CreateAsync_InsertedButNotReReadable_Throws()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(InsertSql, 1);
        connection.RespondToQuery(GetByIdSql, new DataTable());
        var customer = new CoreCustomer { CustomerId = Guid.NewGuid(), Name = "Test Customer", IsActive = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(customer));
    }

    [Fact]
    public async Task UpdateAsync_ExistingCustomer_UpdatesAndReReads()
    {
        var (repository, connection) = CreateRepository();
        var customerId = Guid.NewGuid();
        connection.RespondToNonQuery(UpdateSql, 1);
        connection.RespondToQuery(GetByIdSql, CustomerTable(customerId));
        var customer = new CoreCustomer { CustomerId = customerId, Name = "Test Customer", IsActive = true };

        var result = await repository.UpdateAsync(customer);

        Assert.NotNull(result);
        Assert.Equal(customerId, result!.CustomerId);
    }

    [Fact]
    public async Task UpdateAsync_UnknownCustomer_ReturnsNull()
    {
        var (repository, connection) = CreateRepository();
        connection.RespondToNonQuery(UpdateSql, 0);
        var customer = new CoreCustomer { CustomerId = Guid.NewGuid(), Name = "Test Customer" };

        var result = await repository.UpdateAsync(customer);

        Assert.Null(result);
    }
}
