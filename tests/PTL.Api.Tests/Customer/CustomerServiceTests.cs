using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Customer;
using PTL.Core.Customer;

namespace PTL.Api.Tests.Customer;

public class CustomerServiceTests
{
    private static CustomerService CreateService(FakeCustomerRepository repository) =>
        new(repository, NullLogger<CustomerService>.Instance);

    private static PTL.Core.Customer.Customer ValidActiveCustomer(string name = "Sample Laboratories Ltd") => new()
    {
        Name = name,
        CustomerTypeId = Guid.NewGuid(),
        ContactName = "Alice Example",
        Organisation = name,
        Address1 = "1 Sample Street",
        Address2 = "Sample District",
        Telephone = "01234 567890",
        Email = "alice@example.com",
        InvoiceOrganisation = name,
        InvoiceAddress1 = "1 Sample Street",
        InvoiceAddress2 = "Sample District",
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

    [Fact]
    public async Task DeactivateCustomerAsync_SetsInactiveAndStampsInactiveDate()
    {
        var repository = new FakeCustomerRepository();
        var service = CreateService(repository);
        var created = await service.CreateCustomerAsync(ValidActiveCustomer());
        var statusId = Guid.NewGuid();

        var deactivated = await service.DeactivateCustomerAsync(created.CustomerId, statusId);

        Assert.NotNull(deactivated);
        Assert.False(deactivated!.IsActive);
        Assert.Equal(statusId, deactivated.CustomerStatusId);
        Assert.NotNull(deactivated.InactiveDate);
    }

    [Fact]
    public async Task DeactivateCustomerAsync_UnknownCustomer_ReturnsNull()
    {
        var service = CreateService(new FakeCustomerRepository());

        var result = await service.DeactivateCustomerAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ReactivateCustomerAsync_ClearsInactiveDateAndCustomerStatusId()
    {
        var repository = new FakeCustomerRepository();
        var service = CreateService(repository);
        var created = await service.CreateCustomerAsync(ValidActiveCustomer());
        await service.DeactivateCustomerAsync(created.CustomerId, Guid.NewGuid());

        var reactivated = await service.ReactivateCustomerAsync(created.CustomerId);

        Assert.NotNull(reactivated);
        Assert.True(reactivated!.IsActive);
        Assert.Null(reactivated.InactiveDate);
        Assert.Null(reactivated.CustomerStatusId);
    }

    [Fact]
    public async Task ReactivateCustomerAsync_UnknownCustomer_ReturnsNull()
    {
        var service = CreateService(new FakeCustomerRepository());

        var result = await service.ReactivateCustomerAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
