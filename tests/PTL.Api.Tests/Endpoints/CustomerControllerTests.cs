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
    private static CustomerController CreateController(FakeCustomerRepository repository)
    {
        var controller = new CustomerController(new CustomerService(repository, NullLogger<CustomerService>.Instance), NullLogger<CustomerController>.Instance);

        // ValidationProblem() resolves ProblemDetailsFactory from HttpContext.RequestServices,
        // which a bare controller instance does not have without this wiring.
        var services = new ServiceCollection().AddMvc().Services.BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };

        return controller;
    }

    private static CreateCustomerRequest ValidCreateRequest(string name = "Sample Laboratories Ltd") => new(
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
        InvoiceEmail: string.Empty,
        IsActive: true,
        CanOrderOnline: false,
        CustomerStatusId: null);

    private static UpdateCustomerRequest ToUpdateRequest(CreateCustomerRequest request) => new(
        request.RegisteredFileNumber, request.Name, request.PreviousName, request.CustomerTypeId, request.VatNumber,
        request.VatRatingId, request.AccountNumber, request.CustomerFinanceId, request.ContactName, request.Organisation,
        request.Address1, request.Address2, request.Address3, request.Address4, request.Address5, request.CountryId,
        request.Telephone, request.Telephone2, request.Fax, request.Email, request.CurrencyId, request.Comments,
        request.PostageArrangements, request.PaymentNonUK, request.InvoiceName, request.InvoiceOrganisation,
        request.InvoiceAddress1, request.InvoiceAddress2, request.InvoiceAddress3, request.InvoiceAddress4,
        request.InvoiceAddress5, request.InvoiceCountryId, request.InvoiceTelephone, request.InvoiceTelephone2,
        request.InvoiceFax, request.InvoiceEmail, request.IsActive, request.CanOrderOnline, request.CustomerStatusId);

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

        var badRequest = Assert.IsAssignableFrom<ObjectResult>(result.Result);
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
        var summaries = Assert.IsAssignableFrom<IReadOnlyList<CustomerSummaryResponse>>(ok.Value);
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

        var badRequest = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task DeactivateCustomer_UnknownCustomer_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerRepository());

        var result = await controller.DeactivateCustomer(Guid.NewGuid(), new DeactivateCustomerRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeactivateCustomer_ExistingCustomer_ReturnsOkWithInactiveCustomer()
    {
        var repository = new FakeCustomerRepository();
        var controller = CreateController(repository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customerId = ((CustomerResponse)((CreatedAtActionResult)created.Result!).Value!).CustomerId;

        var result = await controller.DeactivateCustomer(customerId, new DeactivateCustomerRequest(Guid.NewGuid()), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.False(((CustomerResponse)ok.Value!).IsActive);
    }

    [Fact]
    public async Task ReactivateCustomer_UnknownCustomer_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCustomerRepository());

        var result = await controller.ReactivateCustomer(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ReactivateCustomer_ExistingCustomer_ReturnsOkWithActiveCustomer()
    {
        var repository = new FakeCustomerRepository();
        var controller = CreateController(repository);
        var created = await controller.CreateCustomer(ValidCreateRequest(), CancellationToken.None);
        var customerId = ((CustomerResponse)((CreatedAtActionResult)created.Result!).Value!).CustomerId;
        await controller.DeactivateCustomer(customerId, new DeactivateCustomerRequest(Guid.NewGuid()), CancellationToken.None);

        var result = await controller.ReactivateCustomer(customerId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(((CustomerResponse)ok.Value!).IsActive);
    }
}
