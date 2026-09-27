using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Contract;

public class ContractControllerTests
{
    private static PTL.InternalWeb.Features.Contract.ContractController CreateController(FakeContractApiClient apiClient, FakeCustomerApiClient? customerApiClient = null, FakeLookupApiClient? lookupApiClient = null, FakeImportPermitApiClient? importPermitApiClient = null, FakeContractDocumentService? documentService = null, FakeContractExportApiClient? contractExportApiClient = null) =>
        new(apiClient, customerApiClient ?? new FakeCustomerApiClient(), lookupApiClient ?? new FakeLookupApiClient(), importPermitApiClient ?? new FakeImportPermitApiClient(), NullLogger<PTL.InternalWeb.Features.Contract.ContractController>.Instance, documentService ?? new FakeContractDocumentService(), contractExportApiClient ?? new FakeContractExportApiClient());

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
    public async Task Export_ExistingContract_GeneratesDocxDownload()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var apiClient = new FakeContractApiClient
        {
            ContractResponse = SampleContract(contractId, customerId),
            ItemsResponse = new ContractItemsResponse(
                contractId,
                "A",
                DateTime.UtcNow.Year,
                "QAL/00001",
                "£",
                0m,
                10m,
                0,
                0m,
                0m,
                0,
                0m,
                0m,
                0,
                0m,
                0m,
                0m,
                0m,
                0m,
                false,
                [])
        };

        var customerApiClient = new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) };
        var documentService = new FakeContractDocumentService
        {
            Response = new ContractDocumentResponse("Contract-ABC.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50, 0x4B, 0x03, 0x04])
        };
        var controller = new PTL.InternalWeb.Features.Contract.ContractController(
            apiClient,
            customerApiClient,
            new FakeLookupApiClient(),
            new FakeImportPermitApiClient(),
            NullLogger<PTL.InternalWeb.Features.Contract.ContractController>.Instance,
            documentService,
            new FakeContractExportApiClient());

        var result = await controller.Export(contractId, "Contract", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("Contract-ABC.docx", file.FileDownloadName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", file.ContentType);
        Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, file.FileContents);
    }

    [Fact]
    public async Task Details_UnknownContract_ReturnsNotFound()
    {
        var controller = CreateController(new FakeContractApiClient { ContractResponse = null });

        var result = await controller.Details(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Export_UnknownDocumentType_ReturnsFeatureNotAvailable()
    {
        var contractId = Guid.NewGuid();
        var controller = CreateController(new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) });

        var result = await controller.Export(contractId, "Something Else", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("FeatureNotAvailable", view.ViewName);
    }

    [Fact]
    public async Task Export_UnknownContract_ReturnsNotFound()
    {
        var controller = CreateController(new FakeContractApiClient { ContractResponse = null });

        var result = await controller.Export(Guid.NewGuid(), "Contract", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Export_MissingTemplate_FallsBackToFeatureNotAvailable()
    {
        var contractId = Guid.NewGuid();
        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) },
            documentService: new FakeContractDocumentService { ExceptionToThrow = new FileNotFoundException("no template") });

        var result = await controller.Export(contractId, "Contract", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("FeatureNotAvailable", view.ViewName);
    }

    [Fact]
    public async Task Export_MapsLegacyMergeFieldsAndContractItemsRegion()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var vatRatingId = Guid.NewGuid();

        var apiClient = new FakeContractApiClient
        {
            ContractResponse = SampleContract(contractId, customerId),
            ItemsResponse = new ContractItemsResponse(
                contractId, "A", DateTime.UtcNow.Year, "QAL/00001", "£", 0.1m, 25m,
                2, 5m, 10m, 3, 4m, 12m, 1, 6m, 6m, 9.5m, 85m, 95.5m, false,
                [
                    new ContractItemSchemeResponse(schemeId, "S1", "Salmonella",
                    [
                        new ContractItemResponse(Guid.NewGuid(), Guid.NewGuid(), "LAB1", "Lab One", "Lab One Ltd", 4, 42.5m, false, false)
                    ])
                ])
        };

        var customer = SampleCustomer(customerId) with
        {
            AccountNumber = "ACC-1",
            VatNumber = "GB123",
            VatRatingId = vatRatingId,
            CountryId = countryId,
            ContactName = "Alice Example"
        };

        var documentService = new FakeContractDocumentService
        {
            Response = new ContractDocumentResponse("Contract-QAL00001A.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50])
        };

        var controller = CreateController(
            apiClient,
            new FakeCustomerApiClient { CustomerResponse = customer },
            new FakeLookupApiClient
            {
                Countries = [new PTL.Contracts.Lookup.CountryResponse(countryId, "United Kingdom")],
                VatRatings = [new PTL.Contracts.Lookup.VatRatingResponse(vatRatingId, "Standard")]
            },
            documentService: documentService);

        await controller.Export(contractId, "Contract", CancellationToken.None);

        var request = Assert.IsType<ContractDocumentRequest>(documentService.LastRequest);
        Assert.Equal("Contract", request.DocumentType);
        Assert.Equal("ContractExampleTemplate", request.TemplateName);
        Assert.Equal("QAL/00001", request.MergeValues["ContractNumber"]);
        Assert.Equal("ACC-1", request.MergeValues["AccountNumber"]);
        Assert.Equal("Standard", request.MergeValues["VatRating"]);
        Assert.Equal("United Kingdom", request.MergeValues["Country"]);
        Assert.Equal("£25.00", request.MergeValues["AdminCharge"]);
        Assert.Equal("£95.50", request.MergeValues["ContractTotal"]);
        Assert.Equal("10.00", request.MergeValues["DiscountRate"]);

        var rows = Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyDictionary<string, string>>>(
            request.Regions!["ContractItems"]);
        var row = Assert.Single(rows);
        Assert.Equal("Salmonella", row["SchemeName"]);
        Assert.Equal("Lab One Ltd", row["ParticipantName"]);
        Assert.Equal("4", row["NumberOfDistributions"]);
        Assert.Equal("£42.50", row["Price"]);
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
