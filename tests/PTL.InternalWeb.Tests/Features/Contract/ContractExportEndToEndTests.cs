using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
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

/// <summary>
/// Exercises the real merge service behind
/// <see cref="PTL.InternalWeb.Features.Contract.ContractController.Export"/> against the template
/// selected in <c>tblUploadedTemplate</c> - only the API clients are faked - so a break anywhere in
/// the chain fails here rather than only in production.
/// </summary>
public class ContractExportEndToEndTests
{
    [Fact]
    public async Task Export_MergesTheSelectedUploadedTemplate()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var selectedFileId = Guid.NewGuid();

        var templateApiClient = new FakeExportTemplateApiClient()
            .WithSelectedTemplate(ExportDocumentTypes.Contracts, selectedFileId, "QM092Ed4Contract120110.docx", MergeTemplateFactory.ContractTemplate());

        var controller = CreateController(contractId, customerId, templateApiClient);

        var result = await controller.Export(contractId, "Contract", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(ExportTemplateService.DocxContentType, file.ContentType);
        Assert.Equal("Contract-QAL00001A.docx", file.FileDownloadName);

        // The selected row drove the download, not a filename convention.
        Assert.Equal(selectedFileId, templateApiClient.LastDownloadedFileId);

        using var stream = new MemoryStream(file.FileContents);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart!.Document!.Body!;

        Assert.Contains("QAL/00001", body.InnerText, StringComparison.Ordinal);
        Assert.Contains("Sample Laboratories Ltd", body.InnerText, StringComparison.Ordinal);
        Assert.DoesNotContain("MERGEFIELD", body.InnerText, StringComparison.OrdinalIgnoreCase);

        var rows = body.Descendants<TableRow>().ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains("Salmonella", rows[1].InnerText, StringComparison.Ordinal);
        Assert.Contains("\u00a342.50", rows[1].InnerText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_NoSelectedTemplate_ReportsTheLegacyError()
    {
        var contractId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // Nothing selected for this document type - legacy's istemplateUploaded = False branch.
        var controller = CreateController(contractId, customerId, new FakeExportTemplateApiClient());

        var result = await controller.Export(contractId, "Contract", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(customerId, redirect.RouteValues!["customerId"]);

        var notification = controller.TempData.GetNotification();
        Assert.Equal(NotificationType.Error, notification?.Type);
        Assert.Equal("Template file not found", notification?.Message);
    }

    private static PTL.InternalWeb.Features.Contract.ContractController CreateController(
        Guid contractId,
        Guid customerId,
        FakeExportTemplateApiClient templateApiClient)
    {
        var apiClient = new FakeContractApiClient
        {
            ContractResponse = new ContractResponse(
                contractId, customerId, "Sample Laboratories Ltd", "QAL/00001", DateTime.UtcNow.Year, "UT3/306",
                string.Empty, "Alice Example", string.Empty, string.Empty, 0.1m, 25m, 2, 5m, 3, 4m, 1, 6m,
                null, null, null, string.Empty, null, true, false, "A", DateTime.UtcNow, string.Empty, false, false, false, null, null),
            ItemsResponse = new ContractItemsResponse(
                contractId, "A", DateTime.UtcNow.Year, "QAL/00001", "\u00a3", 0.1m, 25m,
                2, 5m, 10m, 3, 4m, 12m, 1, 6m, 6m, 9.5m, 85m, 95.5m, false,
                [
                    new ContractItemSchemeResponse(Guid.NewGuid(), "S1", "Salmonella",
                    [
                        new ContractItemResponse(Guid.NewGuid(), Guid.NewGuid(), "LAB1", "Lab One", "Lab One Ltd", 4, 42.5m, false, false)
                    ])
                ])
        };

        return new PTL.InternalWeb.Features.Contract.ContractController(
            apiClient,
            new FakeCustomerApiClient { CustomerResponse = SampleCustomer(customerId) },
            new FakeLookupApiClient(),
            new FakeImportPermitApiClient(),
            NullLogger<PTL.InternalWeb.Features.Contract.ContractController>.Instance,
            new FakeContractExportApiClient(),
            new FakeContractRenewalApiClient(),
            templateApiClient,
            new FakeBulkExportApiClient(),
            new TemplateMergeService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };
    }

    private static PTL.Contracts.Customer.CustomerResponse SampleCustomer(Guid customerId) => new(
        CustomerId: customerId, QalNumber: "QAL/00001", RegisteredFileNumber: string.Empty, Name: "Sample Laboratories Ltd",
        PreviousName: string.Empty, CustomerTypeId: Guid.Empty, VatNumber: string.Empty, VatRatingId: Guid.Empty,
        AccountNumber: string.Empty, CustomerFinanceId: string.Empty, ContactName: "Alice Example", Organisation: "Sample Laboratories Ltd",
        Address1: string.Empty, Address2: string.Empty, Address3: string.Empty, Address4: string.Empty, Address5: string.Empty,
        CountryId: Guid.Empty, Telephone: string.Empty, Telephone2: string.Empty, Fax: string.Empty, Email: string.Empty,
        CurrencyId: Guid.Empty, Comments: string.Empty, InitialStartDate: DateTime.UtcNow, PostageArrangements: string.Empty,
        PaymentNonUK: false, InvoiceName: string.Empty, InvoiceOrganisation: string.Empty, InvoiceAddress1: string.Empty,
        InvoiceAddress2: string.Empty, InvoiceAddress3: string.Empty, InvoiceAddress4: string.Empty, InvoiceAddress5: string.Empty,
        InvoiceCountryId: Guid.Empty, InvoiceTelephone: string.Empty, InvoiceTelephone2: string.Empty, InvoiceFax: string.Empty,
        InvoiceEmail: string.Empty, IsActive: true, CanOrderOnline: true, InactiveDate: null, CustomerStatusId: null);
}
