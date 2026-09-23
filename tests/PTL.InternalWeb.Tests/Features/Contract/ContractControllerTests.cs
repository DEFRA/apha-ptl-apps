using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Contract;

public class ContractControllerTests
{
    private static PTL.InternalWeb.Features.Contract.ContractController CreateController(FakeContractApiClient apiClient, FakeCustomerApiClient? customerApiClient = null, FakeLookupApiClient? lookupApiClient = null) =>
        new(apiClient, customerApiClient ?? new FakeCustomerApiClient(), lookupApiClient ?? new FakeLookupApiClient(), NullLogger<PTL.InternalWeb.Features.Contract.ContractController>.Instance);

    private static ContractResponse SampleContract(Guid contractId, Guid customerId, bool isReadOnly = false) => new(
        contractId, customerId, "Sample Laboratories Ltd", "QAL/00001", DateTime.UtcNow.Year + 1, "UT12345",
        string.Empty, "Alice Example", string.Empty, string.Empty, 0, 0, 0, 0, 0, 0, 0, 0,
        DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, string.Empty, DateTime.UtcNow, true, isReadOnly, "A",
        DateTime.UtcNow, string.Empty, false, false, false, null, null);

    [Fact]
    public async Task Index_ReturnsViewWithSearchResults()
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var apiClient = new FakeContractApiClient
        {
            SearchResponse = new ContractSearchResponse([new ContractSummaryResponse(contractId, customerId, DateTime.UtcNow.Year, true, "A")], 1, 1, 20)
        };
        var controller = CreateController(apiClient);

        var result = await controller.Index(customerId, null, ContractPeriodFilter.CurrentAndNext, null, 1, 20, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Contract.ContractListViewModel>(view.Model);
        Assert.Single(model.Contracts);
        Assert.Equal(1, model.TotalCount);
    }

    [Fact]
    public async Task Index_ReturnsCustomerAndYearNames()
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var apiClient = new FakeContractApiClient
        {
            SearchResponse = new ContractSearchResponse([new ContractSummaryResponse(contractId, customerId, 2020, true, "A")], 1, 1, 20)
        };
        var customerApiClient = new FakeCustomerApiClient
        {
            CustomerResponse = SampleCustomer(customerId)
        };
        var lookupApiClient = new FakeLookupApiClient
        {
            AllYears = [new PTL.Contracts.Lookup.YearResponse(2020, "2020/21")]
        };
        var controller = CreateController(apiClient, customerApiClient, lookupApiClient);

        var result = await controller.Index(customerId, null, ContractPeriodFilter.All, null, 1, 20, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Contract.ContractListViewModel>(view.Model);
        Assert.Equal("Sample Laboratories Ltd", model.Customer?.Name);
        Assert.Equal("2020/21", model.YearNames[2020]);
    }

    private static PTL.Contracts.Customer.CustomerResponse SampleCustomer(Guid customerId) => new(
        CustomerId: customerId, QalNumber: "QAL/00001", RegisteredFileNumber: string.Empty, Name: "Sample Laboratories Ltd",
        PreviousName: string.Empty, CustomerTypeId: Guid.Empty, VatNumber: string.Empty, VatRatingId: Guid.Empty,
        AccountNumber: string.Empty, CustomerFinanceId: string.Empty, ContactName: string.Empty, Organisation: "Sample Organisation",
        Address1: string.Empty, Address2: string.Empty, Address3: string.Empty, Address4: string.Empty, Address5: string.Empty,
        CountryId: Guid.Empty, Telephone: string.Empty, Telephone2: string.Empty, Fax: string.Empty, Email: string.Empty,
        CurrencyId: Guid.Empty, Comments: string.Empty, InitialStartDate: DateTime.UtcNow, PostageArrangements: string.Empty,
        PaymentNonUK: false, InvoiceName: string.Empty, InvoiceOrganisation: string.Empty, InvoiceAddress1: string.Empty,
        InvoiceAddress2: string.Empty, InvoiceAddress3: string.Empty, InvoiceAddress4: string.Empty, InvoiceAddress5: string.Empty,
        InvoiceCountryId: Guid.Empty, InvoiceTelephone: string.Empty, InvoiceTelephone2: string.Empty, InvoiceFax: string.Empty,
        InvoiceEmail: string.Empty, IsActive: true, CanOrderOnline: true, InactiveDate: null, CustomerStatusId: null);

    [Fact]
    public async Task Details_UnknownContract_ReturnsNotFound()
    {
        var controller = CreateController(new FakeContractApiClient { ContractResponse = null });

        var result = await controller.Details(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ExistingContract_ReturnsViewWithModel()
    {
        var contractId = Guid.NewGuid();
        var controller = CreateController(new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) });

        var result = await controller.Details(contractId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(contractId, Assert.IsType<ContractResponse>(view.Model).ContractId);
    }

    [Fact]
    public async Task Create_Get_ReturnsFormViewModelWithCustomerId()
    {
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeContractApiClient());

        var result = await controller.Create(customerId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Contract.ContractFormViewModel>(view.Model);
        Assert.Equal(customerId, model.CustomerId);
    }

    [Fact]
    public async Task Create_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var controller = CreateController(new FakeContractApiClient());
        controller.ModelState.AddModelError("YearId", "Enter a year.");
        var model = new PTL.InternalWeb.Features.Contract.ContractFormViewModel();

        var result = await controller.Create(Guid.NewGuid(), model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Create_Post_ApiFailure_AddsErrorsAndReturnsView()
    {
        var apiClient = new FakeContractApiClient
        {
            SaveResult = new PTL.Contracts.Contract.ContractSaveResult(false, null, new Dictionary<string, string[]> { ["UTNumber"] = ["Enter either a UT number or an FT number, but not both."] })
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Contract.ContractFormViewModel { YearId = 2027 };

        var result = await controller.Create(Guid.NewGuid(), model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Create_Post_Success_RedirectsToDetails()
    {
        var contractId = Guid.NewGuid();
        var apiClient = new FakeContractApiClient
        {
            SaveResult = new PTL.Contracts.Contract.ContractSaveResult(true, SampleContract(contractId, Guid.NewGuid()), new Dictionary<string, string[]>())
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Contract.ContractFormViewModel { YearId = 2027, UTNumber = "UT12345" };

        var result = await controller.Create(Guid.NewGuid(), model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(contractId, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task Edit_Get_UnknownContract_ReturnsNotFound()
    {
        var controller = CreateController(new FakeContractApiClient { ContractResponse = null });

        var result = await controller.Edit(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_Post_Success_RedirectsToDetails()
    {
        var contractId = Guid.NewGuid();
        var apiClient = new FakeContractApiClient
        {
            SaveResult = new PTL.Contracts.Contract.ContractSaveResult(true, SampleContract(contractId, Guid.NewGuid()), new Dictionary<string, string[]>())
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Contract.ContractFormViewModel { YearId = 2027, UTNumber = "UT12345" };

        var result = await controller.Edit(contractId, model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(contractId, redirect.RouteValues!["id"]);
    }
}
