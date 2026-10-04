using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Api.Controllers;
using PTL.Api.Tests.Customer;
using PTL.Contracts.Customer;
using PTL.Core.Customer;

namespace PTL.Api.Tests.Endpoints;

public class CustomerControllerTests
{
    private static CustomerController CreateController(FakeCustomerRepository repository, FakePendingCustomerUpdateRepository? pendingRepository = null)
    {
        var controller = new CustomerController(new CustomerService(repository, pendingRepository ?? new FakePendingCustomerUpdateRepository(), NullLogger<CustomerService>.Instance), NullLogger<CustomerController>.Instance);

        // ValidationProblem() resolves ProblemDetailsFactory from HttpContext.RequestServices,
        // which a bare controller instance does not have without this wiring.
        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    private static CustomerSaveRequest ValidCreateRequest(string name = "Sample Laboratories Ltd") => new(
        RegisteredFileNumber: string.Empty,
        Name: name,
        PreviousName: string.Empty,
        CustomerTypeId: Guid.NewGuid(),
        VatNumber: string.Empty,
        VatRatingId: Guid.NewGuid(),
        AccountNumber: string.Empty,
        CustomerFinanceId: string.Empty,
        ContactName: "Alice Example",
        Organisation: name,
        Address1: "1 Sample Street",
        Address2: "Sample District",
        Address3: string.Empty,
        Address4: string.Empty,
        Address5: string.Empty,
        CountryId: Guid.NewGuid(),
        Telephone: "01234 567890",
        Telephone2: string.Empty,
        Fax: string.Empty,
        Email: "alice@example.com",
        CurrencyId: Guid.NewGuid(),
        Comments: string.Empty,
        PostageArrangements: string.Empty,
        PaymentNonUK: false,
        InvoiceName: string.Empty,
        InvoiceOrganisation: name,
        InvoiceAddress1: "1 Sample Street",
        InvoiceAddress2: "Sample District",
        InvoiceAddress3: string.Empty,
        InvoiceAddress4: string.Empty,
        InvoiceAddress5: string.Empty,
        InvoiceCountryId: Guid.NewGuid(),
        InvoiceTelephone: string.Empty,
        InvoiceTelephone2: string.Empty,
        InvoiceFax: string.Empty,
        InvoiceEmail: "invoices@example.com",
        IsActive: true,
        CanOrderOnline: false,
        CustomerStatusId: null);

    private static CustomerSaveRequest ToUpdateRequest(CustomerSaveRequest request) => request;

    [Fact]
    public async Task CreateCustomer_ValidRequest_ReturnsCreatedAtAction()
    {
        var controller = CreateController(new FakeCustomerRepository());

        var result = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(CustomerController.GetCustomer), created.ActionName);
        Assert.IsType<CustomerResponse>(created.Value);
    }

    [Fact]
    public async Task CreateCustomer_InvalidRequest_ReturnsValidationProblem()
    {
        var controller = CreateController(new FakeCustomerRepository());
        var request = ValidCreateRequest() with { Name = string.Empty };

        var result = await controller.CreateCustomer(request, CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result.Result, exactMatch: false);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task GetCustomer_UnknownCustomer_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerRepository());

        var result = await controller.GetCustomer(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetCustomer_ExistingCustomer_ReturnsOk()
    {
        var repository = new FakeCustomerRepository();
        var controller = CreateController(repository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customerId = ((CustomerResponse)((CreatedAtActionResult)created.Result!).Value!).CustomerId;

        var result = await controller.GetCustomer(customerId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(customerId, ((CustomerResponse)ok.Value!).CustomerId);
    }

    [Fact]
    public async Task GetCustomers_ReturnsSummaryList()
    {
        var controller = CreateController(new FakeCustomerRepository());
        await controller.CreateCustomer(ValidCreateRequest("Alpha Labs"), CancellationToken.None);
        await controller.CreateCustomer(ValidCreateRequest("Beta Labs"), CancellationToken.None);

        var result = await controller.GetCustomers(new CustomerRequest(CustomerStatusFilter.Active), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var summaries = Assert.IsType<IReadOnlyList<CustomerSummaryResponse>>(ok.Value, exactMatch: false);
        Assert.Equal(2, summaries.Count);
    }

    [Fact]
    public async Task SearchCustomers_FiltersAndPagesResults()
    {
        var controller = CreateController(new FakeCustomerRepository());
        await controller.CreateCustomer(ValidCreateRequest("Alpha Labs"), CancellationToken.None);
        await controller.CreateCustomer(ValidCreateRequest("Beta Labs"), CancellationToken.None);

        var result = await controller.SearchCustomers(new CustomerSearchRequest("Beta", CustomerStatusFilter.Active, 1, 20), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CustomerSearchResponse>(ok.Value);
        Assert.Single(response.Items);
        Assert.Equal("Beta Labs", response.Items[0].Name);
    }

    [Fact]
    public async Task UpdateCustomer_UnknownCustomer_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerRepository());

        var result = await controller.UpdateCustomer(Guid.NewGuid(), ToUpdateRequest(ValidCreateRequest()), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateCustomer_InvalidRequest_ReturnsValidationProblem()
    {
        var repository = new FakeCustomerRepository();
        var controller = CreateController(repository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customerId = ((CustomerResponse)((CreatedAtActionResult)created.Result!).Value!).CustomerId;
        var invalidUpdate = ToUpdateRequest(ValidCreateRequest()) with { Name = string.Empty };

        var result = await controller.UpdateCustomer(customerId, invalidUpdate, CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result.Result, exactMatch: false);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task UpdateCustomer_SetIsActiveFalse_ReturnsOkWithInactiveCustomer()
    {
        var repository = new FakeCustomerRepository();
        var controller = CreateController(repository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customerId = ((CustomerResponse)((CreatedAtActionResult)created.Result!).Value!).CustomerId;
        var inactiveUpdate = ToUpdateRequest(ValidCreateRequest()) with { IsActive = false };

        var result = await controller.UpdateCustomer(customerId, inactiveUpdate, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.False(((CustomerResponse)ok.Value!).IsActive);
    }

    [Fact]
    public async Task UpdateCustomer_SetIsActiveTrueAfterInactive_ReturnsOkWithActiveCustomer()
    {
        var repository = new FakeCustomerRepository();
        var controller = CreateController(repository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customerId = ((CustomerResponse)((CreatedAtActionResult)created.Result!).Value!).CustomerId;
        var inactiveUpdate = ToUpdateRequest(ValidCreateRequest()) with { IsActive = false };
        await controller.UpdateCustomer(customerId, inactiveUpdate, CancellationToken.None);
        var activeUpdate = ToUpdateRequest(ValidCreateRequest()) with { IsActive = true };

        var result = await controller.UpdateCustomer(customerId, activeUpdate, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(((CustomerResponse)ok.Value!).IsActive);
    }

    [Fact]
    public async Task GetPendingCustomerUpdates_ReturnsSummaries()
    {
        var repository = new FakeCustomerRepository();
        var pendingRepository = new FakePendingCustomerUpdateRepository();
        var controller = CreateController(repository, pendingRepository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customerId = ((CustomerResponse)((CreatedAtActionResult)created.Result!).Value!).CustomerId;
        pendingRepository.Seed(new PendingCustomerUpdate { PendingCustomerUpdateId = Guid.NewGuid(), CustomerId = customerId, IsSubmitted = true });

        var result = await controller.GetPendingCustomerUpdates(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var summaries = Assert.IsType<List<PendingCustomerUpdateSummaryResponse>>(ok.Value);
        Assert.Single(summaries);
        Assert.Equal(customerId, summaries[0].CustomerId);
    }

    [Fact]
    public async Task GetPendingCustomerUpdate_NoPendingUpdate_ReturnsNotFound()
    {
        var repository = new FakeCustomerRepository();
        var controller = CreateController(repository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customerId = ((CustomerResponse)((CreatedAtActionResult)created.Result!).Value!).CustomerId;

        var result = await controller.GetPendingCustomerUpdate(customerId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetPendingCustomerUpdate_PendingUpdateExists_ReturnsComparison()
    {
        var repository = new FakeCustomerRepository();
        var pendingRepository = new FakePendingCustomerUpdateRepository();
        var controller = CreateController(repository, pendingRepository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customerId = ((CustomerResponse)((CreatedAtActionResult)created.Result!).Value!).CustomerId;
        pendingRepository.Seed(new PendingCustomerUpdate { PendingCustomerUpdateId = Guid.NewGuid(), CustomerId = customerId, ContactName = "New Contact", IsSubmitted = true });

        var result = await controller.GetPendingCustomerUpdate(customerId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var comparison = Assert.IsType<PendingCustomerUpdateComparisonResponse>(ok.Value);
        Assert.Equal(customerId, comparison.Current.CustomerId);
        Assert.Equal("New Contact", comparison.Pending.ContactName);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdate_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerRepository());

        var result = await controller.ApprovePendingCustomerUpdate(Guid.NewGuid(), request: null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdate_PendingUpdateExists_AppliesChangesAndReturnsNoContent()
    {
        var repository = new FakeCustomerRepository();
        var pendingRepository = new FakePendingCustomerUpdateRepository();
        var controller = CreateController(repository, pendingRepository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customer = (CustomerResponse)((CreatedAtActionResult)created.Result!).Value!;
        pendingRepository.Seed(ValidPendingUpdate(customer, "New Contact"));

        var result = await controller.ApprovePendingCustomerUpdate(customer.CustomerId, request: null, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var updated = await controller.GetCustomer(customer.CustomerId, CancellationToken.None);
        Assert.Equal("New Contact", ((CustomerResponse)((OkObjectResult)updated.Result!).Value!).ContactName);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdate_AmendedValuesSupplied_AppliesAmendedValues()
    {
        var repository = new FakeCustomerRepository();
        var pendingRepository = new FakePendingCustomerUpdateRepository();
        var controller = CreateController(repository, pendingRepository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customer = (CustomerResponse)((CreatedAtActionResult)created.Result!).Value!;
        pendingRepository.Seed(ValidPendingUpdate(customer, "New Contact"));

        var result = await controller.ApprovePendingCustomerUpdate(customer.CustomerId, AmendedPendingRequest(customer, "Amended Contact"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var updated = await controller.GetCustomer(customer.CustomerId, CancellationToken.None);
        Assert.Equal("Amended Contact", ((CustomerResponse)((OkObjectResult)updated.Result!).Value!).ContactName);
    }

    [Fact]
    public async Task ApprovePendingCustomerUpdate_AmendedValuesBreakBusinessRules_ReturnsValidationProblem()
    {
        var repository = new FakeCustomerRepository();
        var pendingRepository = new FakePendingCustomerUpdateRepository();
        var controller = CreateController(repository, pendingRepository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customer = (CustomerResponse)((CreatedAtActionResult)created.Result!).Value!;
        pendingRepository.Seed(new PendingCustomerUpdate { PendingCustomerUpdateId = Guid.NewGuid(), CustomerId = customer.CustomerId, ContactName = "New Contact", IsSubmitted = true });

        var result = await controller.ApprovePendingCustomerUpdate(customer.CustomerId, AmendedPendingRequest(customer, string.Empty), CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result, exactMatch: false);
        Assert.Equal(400, badRequest.StatusCode);
    }

    private static PendingCustomerUpdateSaveRequest AmendedPendingRequest(CustomerResponse customer, string contactName) => new(
        contactName, customer.Organisation, customer.Address1, customer.Address2, customer.Address3, customer.Address4,
        customer.Address5, customer.CountryId, customer.Telephone, customer.Telephone2, customer.Fax, customer.Email,
        customer.InvoiceName, customer.InvoiceOrganisation, customer.InvoiceAddress1, customer.InvoiceAddress2,
        customer.InvoiceAddress3, customer.InvoiceAddress4, customer.InvoiceAddress5, customer.InvoiceCountryId,
        customer.InvoiceTelephone, customer.InvoiceTelephone2, customer.InvoiceFax, customer.InvoiceEmail);

    // Approve runs CustomerValidator against the resulting customer (legacy mcustomer.IsValid), so
    // a seeded pending update must carry every required field, not just the amended one.
    private static PendingCustomerUpdate ValidPendingUpdate(CustomerResponse customer, string contactName) => new()
    {
        PendingCustomerUpdateId = Guid.NewGuid(),
        CustomerId = customer.CustomerId,
        ContactName = contactName,
        Organisation = customer.Organisation,
        Address1 = customer.Address1,
        Address2 = customer.Address2,
        CountryId = customer.CountryId,
        Telephone = customer.Telephone,
        Email = customer.Email,
        InvoiceOrganisation = customer.InvoiceOrganisation,
        InvoiceAddress1 = customer.InvoiceAddress1,
        InvoiceAddress2 = customer.InvoiceAddress2,
        InvoiceCountryId = customer.InvoiceCountryId,
        InvoiceEmail = customer.InvoiceEmail,
        IsSubmitted = true
    };

    [Fact]
    public async Task DeclinePendingCustomerUpdate_NoPendingUpdate_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerRepository());

        var result = await controller.DeclinePendingCustomerUpdate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeclinePendingCustomerUpdate_PendingUpdateExists_DoesNotChangeCustomerAndReturnsNoContent()
    {
        var repository = new FakeCustomerRepository();
        var pendingRepository = new FakePendingCustomerUpdateRepository();
        var controller = CreateController(repository, pendingRepository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customerId = ((CustomerResponse)((CreatedAtActionResult)created.Result!).Value!).CustomerId;
        pendingRepository.Seed(new PendingCustomerUpdate { PendingCustomerUpdateId = Guid.NewGuid(), CustomerId = customerId, ContactName = "New Contact", IsSubmitted = true });

        var result = await controller.DeclinePendingCustomerUpdate(customerId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        var updated = await controller.GetCustomer(customerId, CancellationToken.None);
        Assert.Equal("Alice Example", ((CustomerResponse)((OkObjectResult)updated.Result!).Value!).ContactName);
    }
}
