using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;
using PTL.Core.Contract.Export.Templates;
using PTL.InternalWeb.Tests.TestSupport;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Tests.Features.Contract;

public class ContractControllerTests
{
    private static PTL.InternalWeb.Features.Contract.ContractController CreateController(FakeContractApiClient apiClient, FakeCustomerApiClient? customerApiClient = null, FakeLookupApiClient? lookupApiClient = null, FakeImportPermitApiClient? importPermitApiClient = null, FakeContractExportApiClient? contractExportApiClient = null, FakeContractRenewalApiClient? contractRenewalApiClient = null, FakeExportTemplateApiClient? exportTemplateApiClient = null, FakeBulkExportApiClient? bulkExportApiClient = null) =>
        new(apiClient, customerApiClient ?? new FakeCustomerApiClient(), lookupApiClient ?? new FakeLookupApiClient(), importPermitApiClient ?? new FakeImportPermitApiClient(), NullLogger<PTL.InternalWeb.Features.Contract.ContractController>.Instance, contractExportApiClient ?? new FakeContractExportApiClient(), contractRenewalApiClient ?? new FakeContractRenewalApiClient(), exportTemplateApiClient ?? new FakeExportTemplateApiClient(), bulkExportApiClient ?? new FakeBulkExportApiClient(), new TemplateMergeService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static FakeExportTemplateApiClient SelectedTemplateFor(string documentType) =>
        new FakeExportTemplateApiClient()
            .WithSelectedTemplate(documentType, Guid.NewGuid(), "Selected.docx", MergeTemplateFactory.ContractTemplate());

    private static ContractResponse SampleContract(Guid contractId, Guid customerId, bool isReadOnly = false) => new(contractId, customerId, "Sample Laboratories Ltd", "QAL/00001", DateTime.UtcNow.Year + 1, "UT12345",
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
        var selectedFileId = Guid.NewGuid();
        var templateApiClient = new FakeExportTemplateApiClient()
            .WithSelectedTemplate(ExportDocumentTypes.Contracts, selectedFileId, "QM092Ed4Contract120110.docx", MergeTemplateFactory.ContractTemplate());

        var controller = CreateController(apiClient, customerApiClient, exportTemplateApiClient: templateApiClient);

        var result = await controller.Export(contractId, "Contract", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("Contract-QAL00001A.docx", file.FileDownloadName);
        Assert.Equal(ExportTemplateService.DocxContentType, file.ContentType);
        Assert.Equal(selectedFileId, templateApiClient.LastDownloadedFileId);
        Assert.NotEmpty(file.FileContents);
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
    public async Task Export_NoSelectedTemplate_ReportsTemplateFileNotFound()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var controller = CreateController(new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) });

        var result = await controller.Export(contractId, "Contract", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(customerId, redirect.RouteValues!["customerId"]);
        Assert.Equal("Template file not found", controller.TempData.GetNotification()?.Message);
    }

    [Fact]
    public async Task Export_SelectedTemplateContentMissing_ReportsFileNotFound()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var templateApiClient = new FakeExportTemplateApiClient()
            .WithSelectedTemplate(ExportDocumentTypes.Contracts, Guid.NewGuid(), "Selected.docx", []);
        templateApiClient.Download = null;

        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) },
            exportTemplateApiClient: templateApiClient);

        var result = await controller.Export(contractId, "Contract", CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("File not found", controller.TempData.GetNotification()?.Message);
    }

    [Fact]
    public async Task Export_SelectedTemplateIsNotADocx_ReportsMailMergeFailure()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var templateApiClient = new FakeExportTemplateApiClient()
            .WithSelectedTemplate(ExportDocumentTypes.Contracts, Guid.NewGuid(), "Legacy.doc", [0x00, 0x01, 0x02, 0x03]);

        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) },
            exportTemplateApiClient: templateApiClient);

        var result = await controller.Export(contractId, "Contract", CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("There was a problem with the Mail Merge", controller.TempData.GetNotification()?.Message);
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

        var templateApiClient = new FakeExportTemplateApiClient()
            .WithSelectedTemplate(
                ExportDocumentTypes.Contracts,
                Guid.NewGuid(),
                "Selected.docx",
                MergeTemplateFactory.ContractTemplate("AccountNumber", "VatRating", "Country", "AdminCharge", "ContractTotal", "DiscountRate"));

        var controller = CreateController(
            apiClient,
            new FakeCustomerApiClient { CustomerResponse = customer },
            new FakeLookupApiClient
            {
                Countries = [new PTL.Contracts.Lookup.CountryResponse(countryId, "United Kingdom")],
                VatRatings = [new PTL.Contracts.Lookup.VatRatingResponse(vatRatingId, "Standard")]
            },
            exportTemplateApiClient: templateApiClient);

        var result = await controller.Export(contractId, "Contract", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        using var stream = new MemoryStream(file.FileContents);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart!.Document!.Body!;
        var text = body.InnerText;

        Assert.Contains("QAL/00001", text, StringComparison.Ordinal);
        Assert.Contains("ACC-1", text, StringComparison.Ordinal);
        Assert.Contains("Standard", text, StringComparison.Ordinal);
        Assert.Contains("United Kingdom", text, StringComparison.Ordinal);
        Assert.Contains("£25.00", text, StringComparison.Ordinal);
        Assert.Contains("£95.50", text, StringComparison.Ordinal);
        Assert.Contains("10.00", text, StringComparison.Ordinal);

        var itemRow = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.TableRow>().Last();
        Assert.Contains("Salmonella", itemRow.InnerText, StringComparison.Ordinal);
        Assert.Contains("£42.50", itemRow.InnerText, StringComparison.Ordinal);
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
    public async Task Create_Post_Success_WithOptionalFieldsPopulated_RedirectsToDetails()
    {
        var contractId = Guid.NewGuid();
        var apiClient = new FakeContractApiClient
        {
            SaveResult = new PTL.Contracts.Contract.ContractSaveResult(true, SampleContract(contractId, Guid.NewGuid()), new Dictionary<string, string[]>())
        };
        var controller = CreateController(apiClient);
        var model = new PTL.InternalWeb.Features.Contract.ContractFormViewModel
        {
            YearId = 2027,
            UTNumber = "UT12345",
            FTNumber = "FT6789",
            ContractSignatory = "Alice Example",
            ActionsRequired = "None",
            RenewalInformation = "Renews annually",
            ReasonForClosure = "N/A",
            Suffix = "B",
            PurchaseOrderNumber = "PO-99"
        };

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
    public async Task RenewContracts_Get_Allowed_WithCustomerAndNonRenewableItem_PopulatesCustomerFieldsAndExcludesIt()
    {
        var customerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var renewableSchemeId = Guid.NewGuid();
        var nonRenewableSchemeId = Guid.NewGuid();
        var contractRenewalApiClient = new FakeContractRenewalApiClient
        {
            ContractsResponse = new RenewableContractsResponse(true, null, ["Alice Example"],
                [new RenewableContractDto(contractId, "A", "Alice Example", "Renewal info", string.Empty, true, 1)]),
            ItemsResponse = new RenewableContractItemsResponse(
            [
                new RenewableContractItemDto(contractId, "A", renewableSchemeId, "LAB1", "Lab One", "S1", "Old Scheme", "S2", "New Scheme", true, "S1"),
                new RenewableContractItemDto(contractId, "A", nonRenewableSchemeId, "LAB2", "Lab Two", "S3", "Old Scheme 2", "S4", "New Scheme 2", false, "S3")
            ])
        };
        var controller = CreateController(
            new FakeContractApiClient(),
            new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) },
            contractRenewalApiClient: contractRenewalApiClient);

        var result = await controller.RenewContracts(customerId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Contract.RenewContractsViewModel>(view.Model);
        Assert.Equal("Sample Laboratories Ltd", model.CustomerName);
        Assert.Equal("Sample Organisation", model.CustomerOrganisation);
        Assert.Equal("QAL/00001", model.QalNumber);
        Assert.Contains(renewableSchemeId, model.SelectedParticipantSchemeIds);
        Assert.DoesNotContain(nonRenewableSchemeId, model.SelectedParticipantSchemeIds);
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
        var controller = CreateController(
            apiClient,
            new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) },
            exportTemplateApiClient: SelectedTemplateFor(ExportDocumentTypes.JobSheets));
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
    public async Task Export_JobSheetWithNonZeroTotalPrice_GeneratesDocument()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var apiClient = new FakeContractApiClient
        {
            ContractResponse = SampleContract(contractId, customerId),
            ItemsResponse = new ContractItemsResponse(
                contractId, "A", DateTime.UtcNow.Year, "QAL/00001", "£",
                0m, 0m, 0, 0m, 0m, 0, 0m, 0m, 0, 0m, 0m, 0m, 0m, 50m, false, [])
        };
        var controller = CreateController(
            apiClient,
            new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) },
            exportTemplateApiClient: SelectedTemplateFor(ExportDocumentTypes.JobSheets));
        controller.TempData = CreateTempData();

        var result = await controller.Export(contractId, "Job Sheet", CancellationToken.None);

        Assert.IsType<FileContentResult>(result);
    }

    [Fact]
    public async Task Export_AddressConfirmationWithNoSampleAddresses_WarnsAndRedirectsToIndex()
    {
        var contractId = Guid.NewGuid();
        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) },
            exportTemplateApiClient: SelectedTemplateFor(ExportDocumentTypes.AddressConfirmationLetters),
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
        var selectedFileId = Guid.NewGuid();
        var templateApiClient = new FakeExportTemplateApiClient()
            .WithSelectedTemplate(ExportDocumentTypes.AddressConfirmationLetters, selectedFileId, "AddressLetter.docx", MergeTemplateFactory.ContractTemplate());
        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) },
            exportTemplateApiClient: templateApiClient,
            contractExportApiClient: new FakeContractExportApiClient { SampleAddresses = [SampleAddress(contractId)] });
        controller.TempData = CreateTempData();

        var result = await controller.Export(contractId, "Address Confirmation", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(ExportTemplateService.DocxContentType, file.ContentType);
        Assert.Equal(selectedFileId, templateApiClient.LastDownloadedFileId);
    }

    [Fact]
    public async Task Export_AddressConfirmationWithMultipleSampleAddresses_MergesThemIntoOneDocument()
    {
        var contractId = Guid.NewGuid();
        var selectedFileId = Guid.NewGuid();
        var templateApiClient = new FakeExportTemplateApiClient()
            .WithSelectedTemplate(ExportDocumentTypes.AddressConfirmationLetters, selectedFileId, "AddressLetter.docx", MergeTemplateFactory.ContractTemplate());
        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) },
            exportTemplateApiClient: templateApiClient,
            contractExportApiClient: new FakeContractExportApiClient { SampleAddresses = [SampleAddress(contractId), SampleAddress(contractId)] });
        controller.TempData = CreateTempData();

        var result = await controller.Export(contractId, "Address Confirmation", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(ExportTemplateService.DocxContentType, file.ContentType);
    }

    [Fact]
    public async Task Export_RenewalLetterWithoutRenewalData_WarnsAndRedirectsToIndex()
    {
        var contractId = Guid.NewGuid();
        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, Guid.NewGuid()) },
            exportTemplateApiClient: SelectedTemplateFor(ExportDocumentTypes.RenewalLetters),
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
        var selectedFileId = Guid.NewGuid();
        var templateApiClient = new FakeExportTemplateApiClient()
            .WithSelectedTemplate(ExportDocumentTypes.RenewalLetters, selectedFileId, "RenewalLetter.docx", MergeTemplateFactory.ContractTemplate());
        var controller = CreateController(
            new FakeContractApiClient { ContractResponse = SampleContract(contractId, customerId) },
            exportTemplateApiClient: templateApiClient,
            contractExportApiClient: new FakeContractExportApiClient { Renewal = SampleRenewal(contractId, customerId) });
        controller.TempData = CreateTempData();

        var result = await controller.Export(contractId, "Renewal Letter", CancellationToken.None);

        Assert.IsType<FileContentResult>(result);
        Assert.Equal(selectedFileId, templateApiClient.LastDownloadedFileId);
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
    public async Task Edit_Get_ExistingContractWithFTNumber_SetsContractTypeToFT()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var contract = SampleContract(contractId, customerId) with { FTNumber = "FT6789" };
        var controller = CreateController(new FakeContractApiClient { ContractResponse = contract });

        var result = await controller.Edit(contractId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PTL.InternalWeb.Features.Contract.ContractFormViewModel>(view.Model);
        Assert.Equal("FT", model.ContractType);
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
