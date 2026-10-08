using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.ApiClient;
using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;
using PTL.Core.Contract.Export.Templates;
using PTL.InternalWeb.Features.Contract;
using PTL.SharedUI.Notifications;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.Contract;

public class ContractControllerExportsTests
{
    private static ContractController CreateController(
        FakeExportTemplateApiClient? exportTemplateApiClient = null,
        FakeBulkExportApiClient? bulkExportApiClient = null) =>
        new(new FakeContractApiClient(), new FakeCustomerApiClient(), new FakeLookupApiClient(),
            new FakeImportPermitApiClient(), NullLogger<ContractController>.Instance,
            new FakeContractExportApiClient(), new FakeContractRenewalApiClient(),
            exportTemplateApiClient ?? new FakeExportTemplateApiClient(),
            bulkExportApiClient ?? new FakeBulkExportApiClient(),
            new TemplateMergeService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider())
        };

    private static ExportTemplateResponse Template(bool selected = false) =>
        new(Guid.NewGuid(), "Contract Template.docx", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            ExportDocumentTypes.Contracts, selected);

    [Fact]
    public void Exports_ListsTheFourLegacyExportPages()
    {
        var view = Assert.IsType<ViewResult>(CreateController().Exports());
        var model = Assert.IsType<ExportsMenuViewModel>(view.Model);

        Assert.Equal(
            ["Export Contracts", "Export Job Sheets", "Export Renewal Letters", "Export Address Confirmation Letters"],
            model.Items.Select(i => i.Text));
    }

    [Fact]
    public async Task ExportContracts_ReturnsTheSharedTemplateView()
    {
        var apiClient = new FakeExportTemplateApiClient
        {
            Templates = new ExportTemplateListResponse(ExportDocumentTypes.Contracts, "Export Contracts", [Template(selected: true)])
        };

        var view = Assert.IsType<ViewResult>(await CreateController(apiClient).ExportContracts());
        var model = Assert.IsType<ExportTemplatesViewModel>(view.Model);

        Assert.Equal("ExportTemplates", view.ViewName);
        Assert.Equal("Export Contracts", model.Title);
        Assert.True(model.HasSelectedTemplate);
        Assert.False(model.IsRenewalLetters);
    }

    [Fact]
    public async Task ExportRenewalLetters_IsFlaggedSoTheViewShowsBothExportButtons()
    {
        var view = Assert.IsType<ViewResult>(await CreateController().ExportRenewalLetters());
        var model = Assert.IsType<ExportTemplatesViewModel>(view.Model);

        Assert.True(model.IsRenewalLetters);
    }

    [Fact]
    public async Task ExportJobSheets_NoTemplates_ShowsTheLegacyEmptyMessage()
    {
        var view = Assert.IsType<ViewResult>(await CreateController().ExportJobSheets());
        var model = Assert.IsType<ExportTemplatesViewModel>(view.Model);

        Assert.Equal("No Mail Merge Templates have been uploaded.", model.Instructions);
        Assert.False(model.HasSelectedTemplate);
    }

    [Fact]
    public async Task UploadExportTemplate_NoFile_ReportsFileNotFoundAndReturnsToTheSameScreen()
    {
        var controller = CreateController();

        var result = await controller.UploadExportTemplate(ExportDocumentTypes.Contracts, file: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ExportContracts", redirect.ActionName);
        Assert.Equal(NotificationType.Error, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task UploadExportTemplate_Failure_SurfacesTheServiceMessage()
    {
        var apiClient = new FakeExportTemplateApiClient
        {
            UploadResponse = new ExportTemplateUploadResponse(false, "A file with this name already exists", null)
        };
        var controller = CreateController(apiClient);

        await controller.UploadExportTemplate(ExportDocumentTypes.JobSheets, FormFile("Duplicate.docx"));

        var notification = controller.TempData.GetNotification()!;
        Assert.Equal(NotificationType.Error, notification.Type);
        Assert.Equal("A file with this name already exists", notification.Message);
    }

    [Fact]
    public async Task UploadExportTemplate_Success_RedirectsToThatDocumentTypesScreen()
    {
        var apiClient = new FakeExportTemplateApiClient();
        var controller = CreateController(apiClient);

        var result = await controller.UploadExportTemplate(ExportDocumentTypes.RenewalLetters, FormFile("Renewal.docx"));

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ExportRenewalLetters", redirect.ActionName);
        Assert.Equal("Renewal.docx", apiClient.LastUploadedFileName);
        Assert.Equal(NotificationType.Success, controller.TempData.GetNotification()!.Type);
    }

    [Fact]
    public async Task UploadExportTemplate_UnknownDocumentType_ReturnsNotFound() =>
        Assert.IsType<NotFoundResult>(await CreateController().UploadExportTemplate("Invoices", FormFile("a.docx")));

    [Fact]
    public async Task UploadExportTemplate_FailureWithoutErrorMessage_UsesFallbackMessage()
    {
        var apiClient = new FakeExportTemplateApiClient
        {
            UploadResponse = new ExportTemplateUploadResponse(false, null, null)
        };
        var controller = CreateController(apiClient);

        await controller.UploadExportTemplate(ExportDocumentTypes.JobSheets, FormFile("Duplicate.docx"));

        var notification = controller.TempData.GetNotification()!;
        Assert.Equal(NotificationType.Error, notification.Type);
        Assert.Equal("File could not be saved", notification.Message);
    }

    // Legacy "Open" streams the template file itself, it never renders a PDF.
    [Fact]
    public async Task DownloadExportTemplate_ReturnsTheStoredFile()
    {
        var apiClient = new FakeExportTemplateApiClient
        {
            Download = new ExportTemplateDownload("Contract Template.docx", ExportTemplateService.DocxContentType, [1, 2, 3])
        };

        var result = await CreateController(apiClient).DownloadExportTemplate(Guid.NewGuid(), ExportDocumentTypes.Contracts);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("Contract Template.docx", file.FileDownloadName);
        Assert.Equal(ExportTemplateService.DocxContentType, file.ContentType);
    }

    [Fact]
    public async Task DownloadExportTemplate_Missing_ReportsFileNotFound()
    {
        var controller = CreateController();

        var result = await controller.DownloadExportTemplate(Guid.NewGuid(), ExportDocumentTypes.Contracts);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("File not found", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task SelectExportTemplate_Success_NotifiesAndReturnsToTheScreen()
    {
        var apiClient = new FakeExportTemplateApiClient();
        var controller = CreateController(apiClient);
        var fileId = Guid.NewGuid();

        var result = await controller.SelectExportTemplate(fileId, ExportDocumentTypes.AddressConfirmationLetters);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ExportAddressConfirmationLetters", redirect.ActionName);
        Assert.Equal(fileId, apiClient.LastSelectedFileId);
        Assert.Equal("Template selected successfully.", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task SelectExportTemplate_Failure_ReportsFileNotFound()
    {
        var apiClient = new FakeExportTemplateApiClient { SelectResult = false };
        var controller = CreateController(apiClient);

        var result = await controller.SelectExportTemplate(Guid.NewGuid(), ExportDocumentTypes.Contracts);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ExportContracts", redirect.ActionName);
        var notification = controller.TempData.GetNotification()!;
        Assert.Equal(NotificationType.Error, notification.Type);
        Assert.Equal("File not found", notification.Message);
    }

    [Fact]
    public async Task DeleteExportTemplate_Success_NotifiesAndReturnsToTheScreen()
    {
        var apiClient = new FakeExportTemplateApiClient();
        var controller = CreateController(apiClient);
        var fileId = Guid.NewGuid();

        await controller.DeleteExportTemplate(fileId, ExportDocumentTypes.Contracts);

        Assert.Equal(fileId, apiClient.LastDeletedFileId);
        Assert.Equal("Template deleted successfully.", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task DeleteExportTemplate_Missing_ReportsFileNotFound()
    {
        var apiClient = new FakeExportTemplateApiClient { DeleteResult = false };
        var controller = CreateController(apiClient);

        await controller.DeleteExportTemplate(Guid.NewGuid(), ExportDocumentTypes.Contracts);

        Assert.Equal("File not found", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task RunExport_NoSelectedTemplate_RefusesWithTheLegacyTooltipText()
    {
        var apiClient = new FakeExportTemplateApiClient
        {
            Templates = new ExportTemplateListResponse(ExportDocumentTypes.Contracts, "Export Contracts", [Template()])
        };
        var controller = CreateController(apiClient);

        var result = await controller.RunExport(ExportDocumentTypes.Contracts);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Please select a Template for use during the Export.", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task RunExport_SelectedTemplateContentMissing_ReportsFileNotFound()
    {
        var templateApiClient = new FakeExportTemplateApiClient
        {
            Templates = new ExportTemplateListResponse(ExportDocumentTypes.Contracts, "Export Contracts", [Template(selected: true)]),
            Download = null
        };
        var controller = CreateController(templateApiClient);

        var result = await controller.RunExport(ExportDocumentTypes.Contracts);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ExportContracts", redirect.ActionName);
        Assert.Equal("File not found", controller.TempData.GetNotification()!.Message);
    }

    [Fact]
    public async Task RunExport_RenewalLetters_RequestsTheUkOrNonUkDataset()
    {
        var selected = Template(selected: true);
        var templateApiClient = new FakeExportTemplateApiClient
        {
            Templates = new ExportTemplateListResponse(ExportDocumentTypes.RenewalLetters, "Export Renewal Letters", [selected]),
            Download = new ExportTemplateDownload("Renewal.docx", ExportTemplateService.DocxContentType, EmptyDocx())
        };
        var bulkApiClient = new FakeBulkExportApiClient();

        var result = await CreateController(templateApiClient, bulkApiClient).RunExport(ExportDocumentTypes.RenewalLetters, nonUk: true);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.True(bulkApiClient.LastNonUk);
        var today = DateTime.Now;
        Assert.Equal($"RenewalLettersNonUKExport_{today.Year}_{today.Month}_{today.Day}.docx", file.FileDownloadName);
    }

    [Fact]
    public async Task RunExport_RenewalLetters_Uk_UsesTheUkFilename()
    {
        var templateApiClient = new FakeExportTemplateApiClient
        {
            Templates = new ExportTemplateListResponse(ExportDocumentTypes.RenewalLetters, "Export Renewal Letters", [Template(selected: true)]),
            Download = new ExportTemplateDownload("Renewal.docx", ExportTemplateService.DocxContentType, EmptyDocx())
        };

        var result = await CreateController(templateApiClient).RunExport(ExportDocumentTypes.RenewalLetters, nonUk: false);

        var file = Assert.IsType<FileContentResult>(result);
        var today = DateTime.Now;
        Assert.Equal($"RenewalLettersUKExport_{today.Year}_{today.Month}_{today.Day}.docx", file.FileDownloadName);
    }

    [Fact]
    public async Task RunExport_JobSheets_UsesTheJobSheetFilename()
    {
        var templateApiClient = new FakeExportTemplateApiClient
        {
            Templates = new ExportTemplateListResponse(ExportDocumentTypes.JobSheets, "Export Job Sheets", [Template(selected: true)]),
            Download = new ExportTemplateDownload("JobSheet.docx", ExportTemplateService.DocxContentType, EmptyDocx())
        };

        var result = await CreateController(templateApiClient).RunExport(ExportDocumentTypes.JobSheets);

        var file = Assert.IsType<FileContentResult>(result);
        var today = DateTime.Now;
        Assert.Equal($"JobSheetExport{today.Year}_{today.Month}_{today.Day}.docx", file.FileDownloadName);
    }

    [Fact]
    public async Task RunExport_AddressConfirmationLetters_UsesTheLegacyFilename()
    {
        var templateApiClient = new FakeExportTemplateApiClient
        {
            Templates = new ExportTemplateListResponse(ExportDocumentTypes.AddressConfirmationLetters, "Export Address Confirmation Letters", [Template(selected: true)]),
            Download = new ExportTemplateDownload("AddressLetter.docx", ExportTemplateService.DocxContentType, EmptyDocx())
        };
        var bulkApiClient = new FakeBulkExportApiClient
        {
            SampleAddresses = [SampleAddress(Guid.NewGuid())]
        };

        var result = await CreateController(templateApiClient, bulkApiClient).RunExport(ExportDocumentTypes.AddressConfirmationLetters);

        var file = Assert.IsType<FileContentResult>(result);
        var today = DateTime.Now;
        Assert.Equal($"AddressConfirmationLettersExport{today.Year}_{today.Month}_{today.Day}.docx", file.FileDownloadName);
    }

    private static SampleAddressResponse SampleAddress(Guid contractId) => new(
        contractId, Guid.NewGuid(), "QAL/00001", "LAB1", "Alice Example", "Lab One Ltd",
        "1 Test Street", "Testville", string.Empty, string.Empty, string.Empty, "United Kingdom",
        "01234 567890", string.Empty, "alice@example.com", "GB123", "ACC-1", "Standard", "PO-1",
        [], []);

    [Fact]
    public async Task RunExport_Contracts_ReturnsTheLegacyFilename()
    {
        var templateApiClient = new FakeExportTemplateApiClient
        {
            Templates = new ExportTemplateListResponse(ExportDocumentTypes.Contracts, "Export Contracts", [Template(selected: true)]),
            Download = new ExportTemplateDownload("Contract.docx", ExportTemplateService.DocxContentType, EmptyDocx())
        };

        var result = await CreateController(templateApiClient).RunExport(ExportDocumentTypes.Contracts);

        var file = Assert.IsType<FileContentResult>(result);
        var today = DateTime.Now;
        Assert.Equal($"ContractExport_{today.Year}_{today.Month}_{today.Day}.docx", file.FileDownloadName);
    }

    [Fact]
    public async Task RunExport_UnknownDocumentType_ReturnsNotFound() =>
        Assert.IsType<NotFoundResult>(await CreateController().RunExport("Invoices"));

    private static FormFile FormFile(string fileName)
    {
        var content = new MemoryStream([1, 2, 3]);
        return new FormFile(content, 0, content.Length, "file", fileName);
    }

    // A minimal valid .docx so the real merge service can open it.
    private static byte[] EmptyDocx()
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
            stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(
                new DocumentFormat.OpenXml.Wordprocessing.Body());
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }
}
