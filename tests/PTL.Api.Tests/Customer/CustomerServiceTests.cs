using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Customer;
using PTL.Core.Customer;

namespace PTL.Api.Tests.Customer;

public class CustomerServiceTests
{
    private static CustomerService CreateService(FakeCustomerRepository repository, FakePendingCustomerUpdateRepository? pendingRepository = null) =>
        new(repository, pendingRepository ?? new FakePendingCustomerUpdateRepository(), NullLogger<CustomerService>.Instance);

    private static PTL.Core.Customer.Customer ValidActiveCustomer(string name = "Sample Laboratories Ltd") => new()
    {
        Name = name,
        CustomerTypeId = Guid.NewGuid(),
        ContactName = "Alice Example",
        Organisation = name,
        Address1 = "1 Sample Street",
        Address2 = "Sample District",
        CountryId = Guid.NewGuid(),
        Telephone = "01234 567890",
        Email = "alice@example.com",
        InvoiceOrganisation = name,
        InvoiceAddress1 = "1 Sample Street",
        InvoiceAddress2 = "Sample District",
        InvoiceCountryId = Guid.NewGuid(),
        InvoiceEmail = "invoices@example.com",
        IsActive = true
    };

    [Fact]
    public async Task CreateCustomerAsync_ValidCustomer_PersistsAndAssignsServerGeneratedFields()
    {
        var service = CreateService(new FakeCustomerRepository());

        var created = await service.CreateCustomerAsync(ValidActiveCustomer());

        Assert.NotEqual(Guid.Empty, created.CustomerId);
        Assert.NotEqual(default, created.InitialStartDate);
        Assert.StartsWith("QAL/", created.QalNumber);
    }

    [Fact]
    public async Task CreateCustomerAsync_InvalidCustomer_ThrowsCustomerValidationException()
    {
        var service = CreateService(new FakeCustomerRepository());
        var customer = ValidActiveCustomer();
        customer.Name = string.Empty;

        await Assert.ThrowsAsync<CustomerValidationException>(() => service.CreateCustomerAsync(customer));
    }

    [Fact]
    public async Task UpdateCustomerAsync_UnknownCustomer_ReturnsNull()
    {
        var service = CreateService(new FakeCustomerRepository());

        var result = await service.UpdateCustomerAsync(Guid.NewGuid(), ValidActiveCustomer());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateCustomerAsync_PreservesQalNumberAndInitialStartDate()
    {
        var repository = new FakeCustomerRepository();
        var service = CreateService(repository);
        var created = await service.CreateCustomerAsync(ValidActiveCustomer());

        var updatedFields = ValidActiveCustomer("Renamed Laboratories Ltd");
        var updated = await service.UpdateCustomerAsync(created.CustomerId, updatedFields);

        Assert.NotNull(updated);
        Assert.Equal(created.QalNumber, updated!.QalNumber);
        Assert.Equal(created.InitialStartDate, updated.InitialStartDate);
        Assert.Equal("Renamed Laboratories Ltd", updated.Name);
    }

    [Fact]
    public async Task SearchCustomersAsync_FiltersByStatusAndSearchTermWithPaging()
    {
        var repository = new FakeCustomerRepository();
        var service = CreateService(repository);
        await service.CreateCustomerAsync(ValidActiveCustomer("Alpha Labs"));
        await service.CreateCustomerAsync(ValidActiveCustomer("Beta Labs"));
        var inactive = ValidActiveCustomer("Gamma Labs");
        inactive.IsActive = false;
        inactive.CustomerStatusId = Guid.NewGuid();
        await service.CreateCustomerAsync(inactive);

        var activeResults = await service.SearchCustomersAsync(null, CustomerStatusFilter.Active, page: 1, pageSize: 20);
        var searchResults = await service.SearchCustomersAsync("Beta", CustomerStatusFilter.All, page: 1, pageSize: 20);
        var pagedResults = await service.SearchCustomersAsync(null, CustomerStatusFilter.All, page: 1, pageSize: 1);

        Assert.Equal(2, activeResults.TotalCount);
        Assert.Single(searchResults.Items);
        Assert.Equal("Beta Labs", searchResults.Items[0].Name);
        Assert.Equal(3, pagedResults.TotalCount);
        Assert.Single(pagedResults.Items);
    }

    // Field set mirrors legacy dbo.spgSearchCustomer (Contracts Admin/Search.aspx).
    [Theory]
    [InlineData("Alpha Labs")]
    [InlineData("QAL/00001")]
    [InlineData("ACC-12345")]
    [InlineData("United Kingdom")]
    [InlineData("Alice Example")]
    public async Task SearchCustomersAsync_MatchesEverySearchableField(string searchTerm)
    {
        var repository = new FakeCustomerRepository();
        var service = CreateService(repository);
        var countryId = Guid.NewGuid();
        repository.CountryNames[countryId] = "United Kingdom";

        var match = ValidActiveCustomer("Alpha Labs");
        match.AccountNumber = "ACC-12345";
        match.CountryId = countryId;
        await service.CreateCustomerAsync(match);
        var other = ValidActiveCustomer("Beta Labs");
        other.ContactName = "Bob Other";
        await service.CreateCustomerAsync(other);

        var results = await service.SearchCustomersAsync(searchTerm, CustomerStatusFilter.All, page: 1, pageSize: 20);

        Assert.Single(results.Items);
        Assert.Equal("Alpha Labs", results.Items[0].Name);
    }

    [Fact]
    public async Task SearchCustomersAsync_IsCaseInsensitiveOnTheNewFields()
    {
        var repository = new FakeCustomerRepository();
        var service = CreateService(repository);
        var countryId = Guid.NewGuid();
        repository.CountryNames[countryId] = "United Kingdom";

        var customer = ValidActiveCustomer("Alpha Labs");
        customer.AccountNumber = "ACC-12345";
        customer.CountryId = countryId;
        await service.CreateCustomerAsync(customer);

        Assert.Single((await service.SearchCustomersAsync("acc-123", CustomerStatusFilter.All, 1, 20)).Items);
        Assert.Single((await service.SearchCustomersAsync("united", CustomerStatusFilter.All, 1, 20)).Items);
        Assert.Single((await service.SearchCustomersAsync("alice", CustomerStatusFilter.All, 1, 20)).Items);
    }

    [Fact]
    public async Task SearchCustomersAsync_NoMatch_ReturnsEmptyResult()
    {
        var repository = new FakeCustomerRepository();
        var service = CreateService(repository);
        await service.CreateCustomerAsync(ValidActiveCustomer("Alpha Labs"));

        var result = await service.SearchCustomersAsync("no-such-term", CustomerStatusFilter.All, 1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-5, 500)]
    public async Task SearchCustomersAsync_InvalidPageOrPageSize_NormalisesToDefaults(int page, int pageSize)
    {
        var repository = new FakeCustomerRepository();
        var service = CreateService(repository);
        await service.CreateCustomerAsync(ValidActiveCustomer("Alpha Labs"));

        var result = await service.SearchCustomersAsync(null, CustomerStatusFilter.All, page, pageSize);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task UpdateCustomerAsync_SetIsActiveFalse_StampsInactiveDate()
    {
        var repository = new FakeCustomerRepository();
        var service = CreateService(repository);
        var created = await service.CreateCustomerAsync(ValidActiveCustomer());
        var updatedFields = ValidActiveCustomer();
        updatedFields.IsActive = false;

        var deactivated = await service.UpdateCustomerAsync(created.CustomerId, updatedFields);

        Assert.NotNull(deactivated);
        Assert.False(deactivated!.IsActive);
        Assert.NotNull(deactivated.InactiveDate);
    }

    [Fact]
    public async Task UpdateCustomerAsync_AlreadyInactiveWithInactiveDateSet_DoesNotRestampInactiveDate()
    {
        var repository = new FakeCustomerRepository();
        var service = CreateService(repository);
        var created = await service.CreateCustomerAsync(ValidActiveCustomer());
        var firstDeactivate = ValidActiveCustomer();
        firstDeactivate.IsActive = false;
        var firstResult = await service.UpdateCustomerAsync(created.CustomerId, firstDeactivate);
        var originalInactiveDate = firstResult!.InactiveDate;

        var secondDeactivate = ValidActiveCustomer();
        secondDeactivate.IsActive = false;
        secondDeactivate.InactiveDate = originalInactiveDate;
        var secondResult = await service.UpdateCustomerAsync(created.CustomerId, secondDeactivate);

        Assert.Equal(originalInactiveDate, secondResult!.InactiveDate);
    }

    [Fact]
    public async Task UpdateCustomerAsync_SetIsActiveTrueAfterInactive_ClearsInactiveDateAndCustomerStatusId()
    {
        var repository = new FakeCustomerRepository();
        var service = CreateService(repository);
        var created = await service.CreateCustomerAsync(ValidActiveCustomer());
        var inactiveFields = ValidActiveCustomer();
        inactiveFields.IsActive = false;
        await service.UpdateCustomerAsync(created.CustomerId, inactiveFields);

        var reactivateFields = ValidActiveCustomer();
        reactivateFields.IsActive = true;
        var reactivated = await service.UpdateCustomerAsync(created.CustomerId, reactivateFields);

        Assert.NotNull(reactivated);
        Assert.True(reactivated!.IsActive);
        Assert.Null(reactivated.InactiveDate);
        Assert.Null(reactivated.CustomerStatusId);
    }

    // Approve runs CustomerValidator against the resulting customer (legacy mcustomer.IsValid), so
    // this fixture must satisfy every required-while-active rule, not just the changed fields.
    private static PendingCustomerUpdate SamplePendingUpdate(Guid customerId) => new()
    {
        PendingCustomerUpdateId = Guid.NewGuid(),
        CustomerId = customerId,
        ContactName = "New Contact",
        Organisation = "New Organisation",
        Address1 = "New Address 1",
        Address2 = "New Address 2",
        CountryId = Guid.NewGuid(),
        Telephone = "01234 567890",
        Email = "new@example.com",
        InvoiceOrganisation = "New Organisation",
        InvoiceAddress1 = "New Address 1",
        InvoiceAddress2 = "New Address 2",
        InvoiceEmail = "new-invoice@example.com",
        InvoiceCountryId = Guid.NewGuid(),
        IsSubmitted = true,
        IsDeleted = false
    };

    [Fact]
    public async Task GetPendingCustomerUpdatesAsync_ReturnsOutstandingSummaries()
    {
        var repository = new FakeCustomerRepository();
        var customer = await repository.CreateAsync(ValidActiveCustomer());
        var pendingRepository = new FakePendingCustomerUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(customer.CustomerId));
        var service = CreateService(repository, pendingRepository);

        var summaries = await service.GetPendingCustomerUpdatesAsync();

        Assert.Single(summaries);
        Assert.Equal(customer.CustomerId, summaries[0].CustomerId);
    }

    [Fact]
    public async Task GetPendingCustomerUpdateAsync_NoPendingUpdate_ReturnsNull()
    {
        var repository = new FakeCustomerRepository();
        var customer = await repository.CreateAsync(ValidActiveCustomer());
        var service = CreateService(repository);

        var result = await service.GetPendingCustomerUpdateAsync(customer.CustomerId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPendingCustomerUpdateAsync_PendingUpdateExists_ReturnsCurrentAndPending()
    {
        var repository = new FakeCustomerRepository();
        var customer = await repository.CreateAsync(ValidActiveCustomer());
        var pendingRepository = new FakePendingCustomerUpdateRepository();
        var pending = SamplePendingUpdate(customer.CustomerId);
        pendingRepository.Seed(pending);
        var service = CreateService(repository, pendingRepository);

        var result = await service.GetPendingCustomerUpdateAsync(customer.CustomerId);

        Assert.NotNull(result);
        Assert.Equal(customer.CustomerId, result!.Value.Current.CustomerId);
        Assert.Equal(pending.ContactName, result.Value.Pending.ContactName);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdateAsync_AppliesPendingFieldsAndSoftDeletes()
    {
        var repository = new FakeCustomerRepository();
        var customer = await repository.CreateAsync(ValidActiveCustomer());
        var pendingRepository = new FakePendingCustomerUpdateRepository();
        var pending = SamplePendingUpdate(customer.CustomerId);
        pendingRepository.Seed(pending);
        var service = CreateService(repository, pendingRepository);

        var approved = await service.ApprovePendingCustomerUpdateAsync(customer.CustomerId);

        Assert.True(approved);
        var updatedCustomer = await repository.GetByIdAsync(customer.CustomerId);
        Assert.Equal("New Contact", updatedCustomer!.ContactName);
        Assert.Null(await service.GetPendingCustomerUpdateAsync(customer.CustomerId));
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdateAsync_NoPendingUpdate_ReturnsFalse()
    {
        var repository = new FakeCustomerRepository();
        var customer = await repository.CreateAsync(ValidActiveCustomer());
        var service = CreateService(repository);

        var approved = await service.ApprovePendingCustomerUpdateAsync(customer.CustomerId);

        Assert.False(approved);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdateAsync_EditedFieldsSupplied_AppliesEditedValues()
    {
        var repository = new FakeCustomerRepository();
        var customer = await repository.CreateAsync(ValidActiveCustomer());
        var pendingRepository = new FakePendingCustomerUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(customer.CustomerId));
        var service = CreateService(repository, pendingRepository);
        var edited = SamplePendingUpdate(customer.CustomerId);
        edited.ContactName = "Amended Contact";

        var approved = await service.ApprovePendingCustomerUpdateAsync(customer.CustomerId, edited);

        Assert.True(approved);
        var updatedCustomer = await repository.GetByIdAsync(customer.CustomerId);
        Assert.Equal("Amended Contact", updatedCustomer!.ContactName);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdateAsync_ResultingCustomerInvalid_ThrowsAndLeavesCustomerUnchanged()
    {
        var repository = new FakeCustomerRepository();
        var customer = await repository.CreateAsync(ValidActiveCustomer());
        var pendingRepository = new FakePendingCustomerUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(customer.CustomerId));
        var service = CreateService(repository, pendingRepository);
        var edited = SamplePendingUpdate(customer.CustomerId);
        edited.ContactName = string.Empty;

        await Assert.ThrowsAsync<CustomerValidationException>(() => service.ApprovePendingCustomerUpdateAsync(customer.CustomerId, edited));

        var unchangedCustomer = await repository.GetByIdAsync(customer.CustomerId);
        Assert.Equal("Alice Example", unchangedCustomer!.ContactName);
    }

    [Fact]
    public async Task DeclinePendingCustomerUpdateAsync_DoesNotChangeLiveCustomer()
    {
        var repository = new FakeCustomerRepository();
        var customer = await repository.CreateAsync(ValidActiveCustomer());
        var pendingRepository = new FakePendingCustomerUpdateRepository();
        pendingRepository.Seed(SamplePendingUpdate(customer.CustomerId));
        var service = CreateService(repository, pendingRepository);

        var declined = await service.DeclinePendingCustomerUpdateAsync(customer.CustomerId);

        Assert.True(declined);
        var unchangedCustomer = await repository.GetByIdAsync(customer.CustomerId);
        Assert.Equal("Alice Example", unchangedCustomer!.ContactName);
        Assert.Null(await service.GetPendingCustomerUpdateAsync(customer.CustomerId));
    }

    [Fact]
    public async Task DeclinePendingCustomerUpdateAsync_NoPendingUpdate_ReturnsFalse()
    {
        var service = CreateService(new FakeCustomerRepository());

        var declined = await service.DeclinePendingCustomerUpdateAsync(Guid.NewGuid());

        Assert.False(declined);
    }
}
