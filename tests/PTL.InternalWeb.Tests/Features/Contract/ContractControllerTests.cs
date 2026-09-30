using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;
using PTL.InternalWeb.Notifications;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Contract;

public class ContractControllerTests
{
    private static PTL.InternalWeb.Features.Contract.ContractController CreateController(FakeContractApiClient apiClient, FakeCustomerApiClient? customerApiClient = null, FakeLookupApiClient? lookupApiClient = null, FakeImportPermitApiClient? importPermitApiClient = null, FakeContractDocumentService? documentService = null, FakeContractExportApiClient? contractExportApiClient = null, FakeContractRenewalApiClient? contractRenewalApiClient = null) =>
        new(apiClient, customerApiClient ?? new FakeCustomerApiClient(), lookupApiClient ?? new FakeLookupApiClient(), importPermitApiClient ?? new FakeImportPermitApiClient(), NullLogger<PTL.InternalWeb.Features.Contract.ContractController>.Instance, documentService ?? new FakeContractDocumentService(), contractExportApiClient ?? new FakeContractExportApiClient(), contractRenewalApiClient ?? new FakeContractRenewalApiClient());

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
            new FakeContractExportApiClient(),
            new FakeContractRenewalApiClient());

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

        var rows = Assert.IsType<IReadOnlyList<IReadOnlyDictionary<string, string>>>(
            request.Regions!["ContractItems"], exactMatch: false);
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

    [Fact]
    public async Task RenewContracts_Get_Allowed_ReturnsViewWithSelectableContractsAndItems()
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var participantSchemeId = Guid.NewGuid();
        var contractRenewalApiClient = new FakeContractRenewalApiClient
        {
            ContractsResponse = new RenewableContractsResponse(true, null, ["Alice Example"],
                [new RenewableContractDto(contractId, "A", "Alice Example", "Renewal info", string.Empty, true, 1)]),
            ItemsResponse = new RenewableContractItemsResponse(
                [new RenewableContractItemDto(contractId, "A", participantSchemeId, "LAB1", "Lab One", "S1", "Old Scheme", "S2", "New Scheme", true, "S1")])
        };
        var controller = CreateController(new FakeContractApiClient(), contractRenewalApiClient: contractRenewalApiClient);

        var result = await controller.RenewContracts(customerId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Contract.RenewContractsViewModel>(view.Model);
        Assert.True(model.IsAllowed);
        Assert.Single(model.Contracts);
        Assert.Single(model.Items);
        Assert.Contains(contractId, model.SelectedContractIds);
        Assert.Contains(participantSchemeId, model.SelectedParticipantSchemeIds);
    }

    [Fact]
    public async Task RenewContracts_Get_NotAllowed_ReturnsViewWithBlockedReasonAndNoItems()
    {
        var contractRenewalApiClient = new FakeContractRenewalApiClient
        {
            ContractsResponse = new RenewableContractsResponse(false, "There are no contracts for the current year.", [], [])
        };
        var controller = CreateController(new FakeContractApiClient(), contractRenewalApiClient: contractRenewalApiClient);

        var result = await controller.RenewContracts(Guid.NewGuid(), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Contract.RenewContractsViewModel>(view.Model);
        Assert.False(model.IsAllowed);
        Assert.Equal("There are no contracts for the current year.", model.BlockedReason);
        Assert.Empty(model.Items);
    }

    [Fact]
    public async Task RenewContracts_Post_Success_RedirectsToIndex()
    {
        var customerId = Guid.NewGuid();
        var contractRenewalApiClient = new FakeContractRenewalApiClient { RenewResult = new RenewContractResponse(true, Guid.NewGuid(), null) };
        var controller = CreateController(new FakeContractApiClient(), contractRenewalApiClient: contractRenewalApiClient);

        var result = await controller.RenewContracts(customerId, [Guid.NewGuid()], [Guid.NewGuid()], "Alice Example", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(customerId, redirect.RouteValues!["customerId"]);
    }

    [Fact]
    public async Task RenewContracts_Post_Failure_RedisplaysWithErrorAndPreservedSelections()
    {
        var customerId = Guid.NewGuid();
        var selectedContractId = Guid.NewGuid();
        var selectedParticipantSchemeId = Guid.NewGuid();
        var contractRenewalApiClient = new FakeContractRenewalApiClient
        {
            ContractsResponse = new RenewableContractsResponse(true, null, [], []),
            RenewResult = new RenewContractResponse(false, null, "There are no Items on the selected Contracts.")
        };
        var controller = CreateController(new FakeContractApiClient(), contractRenewalApiClient: contractRenewalApiClient);

        var result = await controller.RenewContracts(customerId, [selectedContractId], [selectedParticipantSchemeId], "Alice Example", CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Contract.RenewContractsViewModel>(view.Model);
        Assert.Equal("There are no Items on the selected Contracts.", model.ErrorMessage);
        Assert.Contains(selectedContractId, model.SelectedContractIds);
        Assert.Contains(selectedParticipantSchemeId, model.SelectedParticipantSchemeIds);
        Assert.Equal("Alice Example", model.NewContractSignatory);
    }

    [Fact]
    public async Task RenewContracts_Post_NullSelections_TreatedAsEmptyLists()
    {
        var contractRenewalApiClient = new FakeContractRenewalApiClient { RenewResult = new RenewContractResponse(true, Guid.NewGuid(), null) };
        var controller = CreateController(new FakeContractApiClient(), contractRenewalApiClient: contractRenewalApiClient);

        var result = await controller.RenewContracts(Guid.NewGuid(), null!, null!, null, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public void ManageContracts_ReturnsView()
    {
        var controller = CreateController(new FakeContractApiClient());

        var result = controller.ManageContracts();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task ContractItems_ExistingContract_ReturnsViewWithItemsAndCustomerId()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var apiClient = new FakeContractApiClient
        {
            ContractResponse = SampleContract(contractId, customerId),
            ItemsResponse = EmptyItems(contractId)
        };
        var controller = CreateController(apiClient);

        var result = await controller.ContractItems(contractId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Contract.ContractItemsViewModel>(view.Model);
        Assert.Equal(contractId, model.Items.ContractId);
        Assert.Equal(customerId, model.CustomerId);
    }

    [Fact]
    public async Task ContractItems_UnknownItems_ReturnsNotFound()
    {
        var controller = CreateController(new FakeContractApiClient { ItemsResponse = null });

        var result = await controller.ContractItems(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ContractItems_UnknownContract_ReturnsNotFound()
    {
        var contractId = Guid.NewGuid();
        var controller = CreateController(new FakeContractApiClient
        {
            ItemsResponse = EmptyItems(contractId),
            ContractResponse = null
        });

        var result = await controller.ContractItems(contractId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RemoveContractItem_Success_SetsSuccessNotificationAndRedirects()
    {
        var contractId = Guid.NewGuid();
        var controller = CreateController(new FakeContractApiClient { RemovalResult = new ContractItemRemovalResult(true, false, null) });
        controller.TempData = CreateTempData();

        var result = await controller.RemoveContractItem(contractId, Guid.NewGuid(), CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ContractItems", redirect.ActionName);
        Assert.Equal(contractId, redirect.RouteValues!["id"]);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Success, notification!.Type);
        Assert.Equal("Contract item removed successfully.", notification.Message);
    }

    [Fact]
    public async Task RemoveContractItem_Failure_SetsErrorNotificationAndRedirects()
    {
        var contractId = Guid.NewGuid();
        var controller = CreateController(new FakeContractApiClient { RemovalResult = new ContractItemRemovalResult(false, false, "Item is in use") });
        controller.TempData = CreateTempData();

        var result = await controller.RemoveContractItem(contractId, Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification!.Type);
        Assert.Equal("Item is in use", notification.Message);
    }

    [Fact]
    public async Task RemoveContractItem_FailureWithoutMessage_UsesFallbackMessage()
    {
        var controller = CreateController(new FakeContractApiClient { RemovalResult = new ContractItemRemovalResult(false, true, null) });
        controller.TempData = CreateTempData();

        await controller.RemoveContractItem(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("The contract item could not be removed.", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task Export_JobSheetWithZeroTotalPrice_WarnsAndRedirectsToIndex()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var apiClient = new FakeContractApiClient
        {
            ContractResponse = SampleContract(contractId, customerId),
            ItemsResponse = EmptyItems(contractId)
        };
        var controller = CreateController(apiClient, new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) });
        controller.TempData = CreateTempData();

        var result = await controller.Export(contractId, "Job Sheet", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(customerId, redirect.RouteValues!["customerId"]);
        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Warning, notification!.Type);
        Assert.Equal("Total Price of this contract is 0 or there is no data set.", notification.Message);
    }

    [Fact]
    public async Task Export_AddressConfirmationWithNoSampleAddresses_WarnsAndRedirectsToIndex()
    {
        var contractId = Guid.NewGuid();
        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) },
            contractExportApiClient: new FakeContractExportApiClient { SampleAddresses = [] });
        controller.TempData = CreateTempData();

        var result = await controller.Export(contractId, "Address Confirmation", CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("There is no data set", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task Export_AddressConfirmationWithSampleAddresses_GeneratesDocument()
    {
        var contractId = Guid.NewGuid();
        var documentService = new FakeContractDocumentService
        {
            Response = new ContractDocumentResponse("AddressConfirmation.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50])
        };
        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) },
            documentService: documentService,
            contractExportApiClient: new FakeContractExportApiClient { SampleAddresses = [SampleAddress(contractId)] });
        controller.TempData = CreateTempData();

        var result = await controller.Export(contractId, "Address Confirmation", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("AddressConfirmation.docx", file.FileDownloadName);
        Assert.Equal("AddressConfirmationExampleTemplate", documentService.LastRequest!.TemplateName);
    }

    [Fact]
    public async Task Export_RenewalLetterWithoutRenewalData_WarnsAndRedirectsToIndex()
    {
        var contractId = Guid.NewGuid();
        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) },
            contractExportApiClient: new FakeContractExportApiClient { Renewal = null });
        controller.TempData = CreateTempData();

        var result = await controller.Export(contractId, "Renewal Letter", CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("There is no data set", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task Export_RenewalLetterWithRenewalData_GeneratesDocument()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var documentService = new FakeContractDocumentService
        {
            Response = new ContractDocumentResponse("RenewalLetter.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50])
        };
        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) },
            documentService: documentService,
            contractExportApiClient: new FakeContractExportApiClient { Renewal = SampleRenewal(contractId, customerId) });
        controller.TempData = CreateTempData();

        var result = await controller.Export(contractId, "Renewal Letter", CancellationToken.None);

        Assert.IsType<FileContentResult>(result);
        Assert.Equal("ContractRenewalExampleTemplate", documentService.LastRequest!.TemplateName);
    }

    [Fact]
    public async Task Edit_Get_ExistingContract_ReturnsPopulatedFormViewModel()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) },
            new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) },
            new FakeLookupApiClient { Years = [new PTL.Contracts.Lookup.YearResponse(DateTime.UtcNow.Year + 1, "2026/27")] });

        var result = await controller.Edit(contractId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Contract.ContractFormViewModel>(view.Model);
        Assert.Equal(contractId, model.ContractId);
        Assert.Equal(customerId, model.CustomerId);
        Assert.Single(model.YearOptions);
    }

    [Fact]
    public async Task Edit_Post_InvalidModelState_ReturnsViewWithModel()
    {
        var controller = CreateController(new FakeContractApiClient());
        controller.ModelState.AddModelError("YearId", "Select a year");
        var model = new PTL.InternalWeb.Features.Contract.ContractFormViewModel { CustomerId = Guid.NewGuid() };

        var result = await controller.Edit(Guid.NewGuid(), model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task Edit_Post_ApiFailure_AddsErrorsAndReturnsView()
    {
        var apiClient = new FakeContractApiClient
        {
            SaveResult = new PTL.Contracts.Contract.ContractSaveResult(false, null, new Dictionary<string, string[]> { ["UTNumber"] = ["Enter a UT number"] })
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Contract.ContractFormViewModel { YearId = 2027, UTNumber = "UT3/306", CustomerId = Guid.NewGuid() };

        var result = await controller.Edit(Guid.NewGuid(), model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    private static Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary CreateTempData() =>
        new(new Microsoft.AspNetCore.Http.DefaultHttpContext(), new FakeTempDataProvider());

    private static ContractItemsResponse EmptyItems(Guid contractId) => new(
        contractId, "A", DateTime.UtcNow.Year, "QAL/00001", "£",
        0m, 0m, 0, 0m, 0m, 0, 0m, 0m, 0, 0m, 0m, 0m, 0m, 0m, false, []);

    private static SampleAddressResponse SampleAddress(Guid contractId) => new(
        contractId, Guid.NewGuid(), "QAL/00001", "LAB1", "Alice Example", "Lab One Ltd",
        "1 Test Street", "Testville", string.Empty, string.Empty, string.Empty, "United Kingdom",
        "01234 567890", string.Empty, "alice@example.com", "GB123", "ACC-1", "Standard", "PO-1",
        [], []);

    private static ContractRenewalResponse SampleRenewal(Guid contractId, Guid customerId) => new(
        contractId, customerId, "QAL/00001", "Sample Organisation", "Alice Example",
        "1 Test Street", "Testville", string.Empty, string.Empty, string.Empty, "United Kingdom",
        new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2027, 3, 31, 0, 0, 0, DateTimeKind.Utc),
        "Renewal information");
}
