using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Contract;
using PTL.Core.Contract.Document;
using PTL.Core.Contract.Export.Templates;
using PTL.InternalWeb.Confirmation;
using PTL.InternalWeb.Navigation;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.Contract;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access to Contract functionality. Policies will be added later.
public class ContractController(IContractApiClient contractApiClient, ICustomerApiClient customerApiClient, ILookupApiClient lookupApiClient, IImportPermitApiClient importPermitApiClient, ILogger<ContractController> logger, IContractExportApiClient contractExportApiClient, IContractRenewalApiClient contractRenewalApiClient, IExportTemplateApiClient exportTemplateApiClient, IBulkExportApiClient bulkExportApiClient, ITemplateMergeService templateMergeService) : Controller
{
    // Legacy ExportBase.ShowError text, reused verbatim across upload/open/select/delete/export.
    private const string FileNotFound = "File not found";

    // Legacy ContractList.aspx.vb text when no row for the document type has fldSelectedTemplate = 1.
    private const string NoSelectedTemplate = "Template file not found";

    private const string MailMergeFailed = "There was a problem with the Mail Merge";

    private static readonly Action<ILogger, string, int, Exception?> LogBulkExportedMessage =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(30, nameof(LogBulkExportedMessage)),
            "Bulk export completed: documentType={DocumentType} records={RecordCount}");

    private static readonly Action<ILogger, string, Exception?> LogBulkExportFailedMessage =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(31, nameof(LogBulkExportFailedMessage)),
            "Bulk export failed during mail merge: documentType={DocumentType}");

    private static readonly Action<ILogger, Guid, int?, string?, int, int, Exception?> LogDisplayedContractListMessage =
        LoggerMessage.Define<Guid, int?, string?, int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogDisplayedContractListMessage)),
            "Displayed contract list: customerId={CustomerId} yearId={YearId} searchTerm={SearchTerm} page={Page} totalResults={TotalCount}");

    private static readonly Action<ILogger, Guid, Exception?> LogContractNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(2, nameof(LogContractNotFoundMessage)),
            "Contract {ContractId} not found");

    private static readonly Action<ILogger, Guid, Exception?> LogCreateFailedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(3, nameof(LogCreateFailedMessage)),
            "Create failed for contract under customer {CustomerId}");

    private static readonly Action<ILogger, Guid, Exception?> LogCreatedContractMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(4, nameof(LogCreatedContractMessage)),
            "Created contract {ContractId}");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdateFailedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(5, nameof(LogUpdateFailedMessage)),
            "Update failed for contract {ContractId}");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdatedContractMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(6, nameof(LogUpdatedContractMessage)),
            "Updated contract {ContractId}");

    private static readonly Action<ILogger, Guid, int, Exception?> LogDisplayedContractItemsMessage =
        LoggerMessage.Define<Guid, int>(
            LogLevel.Information,
            new EventId(7, nameof(LogDisplayedContractItemsMessage)),
            "Displayed contract items: contractId={ContractId} totalSchemes={TotalSchemeCount}");

    private static readonly Action<ILogger, Guid, Guid, Exception?> LogRemovedContractItemMessage =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Information,
            new EventId(8, nameof(LogRemovedContractItemMessage)),
            "Removed contract item {ParticipantSchemeId} from contract {ContractId}");

    private static readonly Action<ILogger, Guid, Guid, string?, Exception?> LogRemoveContractItemFailedMessage =
        LoggerMessage.Define<Guid, Guid, string?>(
            LogLevel.Warning,
            new EventId(9, nameof(LogRemoveContractItemFailedMessage)),
            "Failed to remove contract item {ParticipantSchemeId} from contract {ContractId}: {Error}");

    private static readonly Action<ILogger, Guid, int, Exception?> LogDisplayedImportPermitsMessage =
        LoggerMessage.Define<Guid, int>(
            LogLevel.Information,
            new EventId(10, nameof(LogDisplayedImportPermitsMessage)),
            "Displayed import permits: contractId={ContractId} totalPermits={TotalPermitCount}");

    private static readonly Action<ILogger, Guid, string, Exception?> LogExportedContractDocumentMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            new EventId(11, nameof(LogExportedContractDocumentMessage)),
            "Exported contract document: contractId={ContractId} documentType={DocumentType}");

    private static readonly Action<ILogger, Guid, string, Exception?> LogMissingTemplateMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(12, nameof(LogMissingTemplateMessage)),
            "No merge template available for contract {ContractId} document type {DocumentType}");

    private static readonly Action<ILogger, Guid, string, Exception?> LogExportNoDataMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            new EventId(13, nameof(LogExportNoDataMessage)),
            "Export produced no data for contract {ContractId} document type {DocumentType}");

    // Landing page for the Manage Contracts section (moved from the removed Menu feature) -
    // mirrors legacy Contracts Admin/MenuContracts.aspx; the left nav (SideNavigationProvider)
    // supplies the actual section contents.
    public IActionResult ManageContracts() => View();

    public async Task<IActionResult> Index(
        Guid customerId,
        int? yearId,
        ContractPeriodFilter period = ContractPeriodFilter.CurrentAndNext,
        string? searchTerm = null,
        int page = 1,
        int pageSize = PTL.SharedUI.Pagination.PaginationModel.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await contractApiClient.GetContractsForCustomerAsync(customerId, new ContractSearchRequest(yearId, period, searchTerm, page, pageSize), cancellationToken);
        LogDisplayedContractListMessage(logger, customerId, yearId, searchTerm, page, result.TotalCount, null);

        // Matches legacy ContractList.aspx.vb: LblSubTitle.Text (QAL/Name/Organisation) and
        // GetYearNameFromYearId (YearId -> display text, e.g. "2025/26") via a separately fetched
        // all-years list, since historical rows can reference any past year.
        var customer = await customerApiClient.GetCustomerAsync(customerId, cancellationToken);
        var years = await lookupApiClient.GetAllYearsAsync(cancellationToken);
        var yearNames = years.ToDictionary(y => y.YearId, y => y.Year);

        var search = new ContractSearchViewModel(customerId, yearId, period, searchTerm, result.Page, result.PageSize);
        return View(new ContractListViewModel(search, result.TotalCount, result.Items, customer, yearNames));
    }

    // Legacy ContractItems.aspx (priced scheme line items) - see docs/migration/contract-migration.md
    // "Feature Breakdown" Phase 1/3. A single unpaginated page per contract, matching legacy exactly -
    // no search/filtering/paging (legacy has none).
    [HttpGet]
    public async Task<IActionResult> ContractItems(Guid id, CancellationToken cancellationToken = default)
    {
        var items = await contractApiClient.GetContractItemsAsync(id, cancellationToken);
        if (items is null)
        {
            LogContractNotFoundMessage(logger, id, null);
            return NotFound();
        }

        var contract = await contractApiClient.GetContractAsync(id, cancellationToken);
        if (contract is null)
        {
            LogContractNotFoundMessage(logger, id, null);
            return NotFound();
        }

        var model = new ContractItemsViewModel(items, contract.CustomerId);
        ViewData[BreadcrumbRouteValues.CustomerId] = contract.CustomerId;
        ViewData[BreadcrumbRouteValues.ContractId] = id;
        LogDisplayedContractItemsMessage(logger, id, items.Schemes.Count, null);
        return View(model);
    }

    // Explicit REST mutation replacing legacy ContractItems.aspx's deferred "mark removed, save
    // later" flow - removes immediately, then redisplays the Contract Items page.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveContractItem(Guid id, Guid participantSchemeId, string? confirmation, CancellationToken cancellationToken)
    {
        if (!ConfirmationFlow.IsConfirmed(confirmation))
        {
            ViewData[BreadcrumbRouteValues.ContractId] = id;
            return this.ConfirmationPage(
                new ConfirmationPageViewModel
                {
                    Title = "Remove Contract Item",
                    Message = "Are you sure you want to remove this Contract Item?",
                    ConfirmText = "Remove",
                    IsDestructive = true,
                    CancelUrl = Url.Action(nameof(ContractItems), new { id }),
                },
                nameof(ContractItems));
        }

        var result = await contractApiClient.RemoveContractItemAsync(id, participantSchemeId, cancellationToken);
        if (!result.Success)
        {
            LogRemoveContractItemFailedMessage(logger, id, participantSchemeId, result.ErrorMessage, null);
            TempData.SetNotification(NotificationType.Error, result.ErrorMessage ?? "The contract item could not be removed.");
        }
        else
        {
            LogRemovedContractItemMessage(logger, id, participantSchemeId, null);
            TempData.SetNotification(NotificationType.Success, "Contract item removed successfully.");
        }

        return RedirectToAction(nameof(ContractItems), new { id });
    }

    // Replaces legacy ContractList.aspx's MailMergeContract/MailMergeSampleAddressLetter/
    // MailMergeJobSheet/MailMergeRenewalLetter row commands, which streamed a merged Word document
    // back to the browser. Output is DOCX rather than the legacy binary DOC. Import Permit(s) is NOT
    // a document export - see the dedicated ImportPermits action below.
    [HttpGet]
    public async Task<IActionResult> Export(Guid id, string documentType, CancellationToken cancellationToken = default)
    {
        if (!ContractDocumentTypes.TryResolve(documentType, out var canonicalType)
            || !ExportDocumentTypes.TryResolve(documentType, out var storageName, out _))
        {
            return View("FeatureNotAvailable", documentType);
        }

        var contract = await contractApiClient.GetContractAsync(id, cancellationToken);
        if (contract is null)
        {
            LogContractNotFoundMessage(logger, id, null);
            return NotFound();
        }

        // Legacy resolves the selected template before gathering any data and abandons the export
        // outright when there is none, so the same check runs first here.
        var (template, noneSelected) = await LoadSelectedTemplateAsync(storageName, cancellationToken);
        if (template is null)
        {
            LogMissingTemplateMessage(logger, id, canonicalType, null);
            TempData.SetNotification(NotificationType.Error, noneSelected ? NoSelectedTemplate : FileNotFound);
            return RedirectToAction(nameof(Index), new { customerId = contract.CustomerId });
        }

        var context = await BuildDocumentContextAsync(canonicalType, template.FileName, contract, cancellationToken);

        // Legacy no-data guards: MailMergeJobSheet returns False when TotalPrice is 0, and
        // MailMergeSampleAddressLetter/RenewalLetter when their collection yields nothing. Contract
        // deliberately has no guard - its TotalPrice check was commented out in the legacy source.
        var noDataMessage = NoDataMessageFor(canonicalType, context);
        if (noDataMessage is not null)
        {
            LogExportNoDataMessage(logger, id, canonicalType, null);
            TempData.SetNotification(NotificationType.Warning, noDataMessage);
            return RedirectToAction(nameof(Index), new { customerId = contract.CustomerId });
        }

        var request = ContractDocumentMergeMapper.Build(context);

        byte[] merged;
        try
        {
            merged = request.AdditionalDocuments is { Count: > 0 }
                ? templateMergeService.MergeTemplateContentMany(
                    template.Content,
                    [new ContractDocumentMergeData(request.MergeValues, request.Regions), .. request.AdditionalDocuments],
                    cancellationToken)
                : templateMergeService.MergeTemplateContent(template.Content, request.MergeValues, request.Regions, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or FileFormatException)
        {
            LogMissingTemplateMessage(logger, id, canonicalType, ex);
            TempData.SetNotification(NotificationType.Error, MailMergeFailed);
            return RedirectToAction(nameof(Index), new { customerId = contract.CustomerId });
        }

        LogExportedContractDocumentMessage(logger, id, canonicalType, null);
        return File(merged, ExportTemplateService.DocxContentType, request.FileName);
    }

    [HttpGet]
    public IActionResult Exports() => View(new ExportsMenuViewModel(
    [
        new ExportsMenuItem("Export Contracts", nameof(ExportContracts)),
        new ExportsMenuItem("Export Job Sheets", nameof(ExportJobSheets)),
        new ExportsMenuItem("Export Renewal Letters", nameof(ExportRenewalLetters)),
        new ExportsMenuItem("Export Address Confirmation Letters", nameof(ExportAddressConfirmationLetters))
    ]));

    // The four export screens are separate actions rather than one parameterised action so that the
    // side navigation and breadcrumbs, which resolve on controller + action, can tell them apart.
    [HttpGet]
    public Task<IActionResult> ExportContracts(CancellationToken cancellationToken = default) =>
        ExportTemplatesViewAsync(ExportDocumentTypes.Contracts, nameof(ExportContracts), cancellationToken);

    [HttpGet]
    public Task<IActionResult> ExportJobSheets(CancellationToken cancellationToken = default) =>
        ExportTemplatesViewAsync(ExportDocumentTypes.JobSheets, nameof(ExportJobSheets), cancellationToken);

    [HttpGet]
    public Task<IActionResult> ExportRenewalLetters(CancellationToken cancellationToken = default) =>
        ExportTemplatesViewAsync(ExportDocumentTypes.RenewalLetters, nameof(ExportRenewalLetters), cancellationToken);

    [HttpGet]
    public Task<IActionResult> ExportAddressConfirmationLetters(CancellationToken cancellationToken = default) =>
        ExportTemplatesViewAsync(ExportDocumentTypes.AddressConfirmationLetters, nameof(ExportAddressConfirmationLetters), cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadExportTemplate(string documentType, IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (!ExportDocumentTypes.TryResolve(documentType, out var storageName, out _))
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            TempData.SetNotification(NotificationType.Error, FileNotFound);
            return RedirectToAction(ExportActionFor(storageName));
        }

        await using var stream = file.OpenReadStream();
        var result = await exportTemplateApiClient.UploadTemplateAsync(storageName, file.FileName, stream, cancellationToken);

        TempData.SetNotification(
            result.Success ? NotificationType.Success : NotificationType.Error,
            result.Success
                ? "Press the select link to allocate a template. The allocated template is highlighted in the table below:"
                : result.ErrorMessage ?? "File could not be saved");

        return RedirectToAction(ExportActionFor(storageName));
    }

    // Legacy "Open" - downloads the stored template file itself, it never renders a PDF.
    [HttpGet]
    public async Task<IActionResult> DownloadExportTemplate(Guid fileId, string documentType, CancellationToken cancellationToken = default)
    {
        var template = await exportTemplateApiClient.DownloadTemplateAsync(fileId, cancellationToken);
        if (template is not null)
        {
            return File(template.Content, template.ContentType, template.FileName);
        }

        TempData.SetNotification(NotificationType.Error, FileNotFound);
        return RedirectToAction(ExportActionFor(documentType));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectExportTemplate(Guid fileId, string documentType, CancellationToken cancellationToken = default)
    {
        var selected = await exportTemplateApiClient.SelectTemplateAsync(fileId, cancellationToken);

        TempData.SetNotification(
            selected ? NotificationType.Success : NotificationType.Error,
            selected ? "Template selected successfully." : FileNotFound);

        return RedirectToAction(ExportActionFor(documentType));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExportTemplate(Guid fileId, string documentType, string? confirmation, CancellationToken cancellationToken = default)
    {
        if (!ConfirmationFlow.IsConfirmed(confirmation))
        {
            return this.ConfirmationPage(
                new ConfirmationPageViewModel
                {
                    Title = "Delete Template",
                    Message = ExportTemplatesViewModel.DeleteConfirmation,
                    ConfirmText = "Delete",
                    IsDestructive = true,
                    CancelUrl = Url.Action(ExportActionFor(documentType)),
                },
                ExportActionFor(documentType));
        }

        var deleted = await exportTemplateApiClient.DeleteTemplateAsync(fileId, cancellationToken);

        TempData.SetNotification(
            deleted ? NotificationType.Success : NotificationType.Error,
            deleted ? "Template deleted successfully." : FileNotFound);

        return RedirectToAction(ExportActionFor(documentType));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunExport(string documentType, bool nonUk = false, CancellationToken cancellationToken = default)
    {
        if (!ExportDocumentTypes.TryResolve(documentType, out var storageName, out _))
        {
            return NotFound();
        }

        var (template, noneSelected) = await LoadSelectedTemplateAsync(storageName, cancellationToken);
        if (template is null)
        {
            TempData.SetNotification(NotificationType.Error, noneSelected ? ExportTemplatesViewModel.ExportDisabledTooltip : FileNotFound);
            return RedirectToAction(ExportActionFor(storageName));
        }

        var documents = await BuildBulkMergeDataAsync(storageName, nonUk, cancellationToken);

        byte[] merged;
        try
        {
            merged = templateMergeService.MergeTemplateContentMany(template.Content, documents, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or FileFormatException)
        {
            LogBulkExportFailedMessage(logger, storageName, ex);
            TempData.SetNotification(NotificationType.Error, MailMergeFailed);
            return RedirectToAction(ExportActionFor(storageName));
        }

        LogBulkExportedMessage(logger, storageName, documents.Count, null);
        return File(merged, ExportTemplateService.DocxContentType, BulkExportFileName(storageName, nonUk));
    }

    // Legacy template resolution, shared by the per-contract and bulk exports: the one row for this
    // document type with fldSelectedTemplate = 1, whose bytes live in S3. Callers report their own
    // legacy message, so "none selected" is distinguished from "selected but its file is gone".
    private async Task<(ExportTemplateDownload? Template, bool NoneSelected)> LoadSelectedTemplateAsync(
        string storageName,
        CancellationToken cancellationToken)
    {
        var templates = await exportTemplateApiClient.GetTemplatesAsync(storageName, cancellationToken);
        var selected = templates?.Templates.FirstOrDefault(t => t.Selected);
        if (selected is null)
        {
            return (null, true);
        }

        return (await exportTemplateApiClient.DownloadTemplateAsync(selected.FileId, cancellationToken), false);
    }

    private async Task<List<ContractDocumentMergeData>> BuildBulkMergeDataAsync(string storageName, bool nonUk, CancellationToken cancellationToken) =>
        storageName switch
        {
            ExportDocumentTypes.AddressConfirmationLetters =>
                BulkExportMergeMapper.AddressConfirmationLetters(await bulkExportApiClient.GetSampleAddressesAsync(cancellationToken)),
            ExportDocumentTypes.RenewalLetters =>
                BulkExportMergeMapper.RenewalLetters(await bulkExportApiClient.GetRenewalsAsync(nonUk, cancellationToken)),
            _ => BulkExportMergeMapper.Contracts(await bulkExportApiClient.GetContractsAsync(cancellationToken)),
        };

    // Legacy export filenames, including their inconsistent use of the underscore separator.
    private static string BulkExportFileName(string storageName, bool nonUk)
    {
        var today = DateTime.Now;
        var stamp = string.Create(CultureInfo.InvariantCulture, $"{today.Year}_{today.Month}_{today.Day}");

        return storageName switch
        {
            ExportDocumentTypes.JobSheets => $"JobSheetExport{stamp}.docx",
            ExportDocumentTypes.AddressConfirmationLetters => $"AddressConfirmationLettersExport{stamp}.docx",
            ExportDocumentTypes.RenewalLetters => nonUk
                ? $"RenewalLettersNonUKExport_{stamp}.docx"
                : $"RenewalLettersUKExport_{stamp}.docx",
            _ => $"ContractExport_{stamp}.docx",
        };
    }

    private async Task<IActionResult> ExportTemplatesViewAsync(string documentType, string actionName, CancellationToken cancellationToken)
    {
        var templates = await exportTemplateApiClient.GetTemplatesAsync(documentType, cancellationToken);
        var rows = templates?.Templates ?? [];

        return View("ExportTemplates", new ExportTemplatesViewModel(
            documentType,
            templates?.DisplayName ?? ExportDocumentTypes.DisplayNameFor(documentType),
            actionName,
            rows,
            rows.Any(t => t.Selected)));
    }

    private static string ExportActionFor(string documentType)
    {
        ExportDocumentTypes.TryResolve(documentType, out var storageName, out _);

        return storageName switch
        {
            ExportDocumentTypes.JobSheets => nameof(ExportJobSheets),
            ExportDocumentTypes.RenewalLetters => nameof(ExportRenewalLetters),
            ExportDocumentTypes.AddressConfirmationLetters => nameof(ExportAddressConfirmationLetters),
            _ => nameof(ExportContracts)
        };
    }

    // Only the data the requested document actually needs is fetched.
    private async Task<ContractDocumentContext> BuildDocumentContextAsync(
        string canonicalType,
        string templateName,
        ContractResponse contract,
        CancellationToken cancellationToken)
    {
        if (canonicalType == ContractDocumentTypes.AddressConfirmation)
        {
            var sampleAddresses = await contractExportApiClient.GetSampleAddressesAsync(contract.ContractId, cancellationToken);
            return new ContractDocumentContext(canonicalType, templateName, contract, null, null, [], [], sampleAddresses, null);
        }

        if (canonicalType == ContractDocumentTypes.RenewalLetter)
        {
            var renewal = await contractExportApiClient.GetRenewalAsync(contract.ContractId, cancellationToken);
            return new ContractDocumentContext(canonicalType, templateName, contract, null, null, [], [], [], renewal);
        }

        var items = await contractApiClient.GetContractItemsAsync(contract.ContractId, cancellationToken);
        var customer = await customerApiClient.GetCustomerAsync(contract.CustomerId, cancellationToken);
        var countries = await lookupApiClient.GetCountriesAsync(cancellationToken);
        var vatRatings = await lookupApiClient.GetVatRatingsAsync(cancellationToken);

        return new ContractDocumentContext(canonicalType, templateName, contract, items, customer, countries, vatRatings, [], null);
    }

    private static string? NoDataMessageFor(string canonicalType, ContractDocumentContext context) => canonicalType switch
    {
        ContractDocumentTypes.JobSheet when (context.Items?.TotalPrice ?? 0m) == 0m =>
            "Total Price of this contract is 0 or there is no data set.",
        ContractDocumentTypes.AddressConfirmation when context.SampleAddresses.Count == 0 =>
            "There is no data set",
        ContractDocumentTypes.RenewalLetter when context.Renewal is null =>
            "There is no data set",
        _ => null,
    };

    // Legacy ImportPermit.aspx GridViewImportPermits: a CommandField "Actions" column with an Edit
    // button per row; clicking it switches that row into edit mode (Update/Cancel). Only the
    // "Import Permit Received"/"Import Permit Expiry" cells have an EditItemTemplate - "Import
    // Permit Required" has none, so it stays the same read-only, disabled checkbox in both view and
    // edit mode (it is never itself editable - see ImportPermitViewModel.cs).
    [HttpGet]
    public async Task<IActionResult> ImportPermits(Guid id, CancellationToken cancellationToken)
    {
        var permits = await importPermitApiClient.GetImportPermitsAsync(id, cancellationToken);
        LogDisplayedImportPermitsMessage(logger, id, permits.Count, null);
        return ImportPermitsView(ToImportPermitsViewModel(id, permits));
    }

    // Legacy ImportPermit.aspx workflow, driven by the page's submit buttons (command):
    //   edit       - GridViewImportPermits_RowEditing: puts one row into edit mode.
    //   update     - GridViewImportPermits_RowUpdating: applies the edited row to the pending (in-memory)
    //                list only and leaves edit mode. Nothing is written to the database and no
    //                notification is shown.
    //   cancelEdit - RowCancelingEdit: discards the row's edit, keeping other rows' pending changes.
    //   apply/save - btnApply/btnSave: write every row to the database; save then leaves the page.
    //   cancel     - btnCancel: leaves the page, discarding all pending changes.
    // Legacy held the pending list in ViewState; here it is round-tripped via the form (pending).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportPermits(Guid id, List<ImportPermitPendingRow>? pending, string? command, Guid? rowId, bool? editReceived, string? editExpiry, CancellationToken cancellationToken)
    {
        if (string.Equals(command, ImportPermitCommands.Cancel, StringComparison.OrdinalIgnoreCase))
        {
            return await RedirectToContractListAsync(id, cancellationToken);
        }

        var model = await BuildPendingImportPermitsModelAsync(id, pending ?? [], cancellationToken);

        if (string.Equals(command, ImportPermitCommands.Edit, StringComparison.OrdinalIgnoreCase))
        {
            model.EditParticipantSchemeId = rowId;
            return ImportPermitsView(model);
        }

        if (string.Equals(command, ImportPermitCommands.Update, StringComparison.OrdinalIgnoreCase))
        {
            return ApplyImportPermitRowEdit(model, rowId, editReceived, editExpiry);
        }

        if (string.Equals(command, ImportPermitCommands.Apply, StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, ImportPermitCommands.Save, StringComparison.OrdinalIgnoreCase))
        {
            var saved = await SaveImportPermitsAsync(model, cancellationToken);
            if (saved && string.Equals(command, ImportPermitCommands.Save, StringComparison.OrdinalIgnoreCase))
            {
                return await RedirectToContractListAsync(id, cancellationToken);
            }

            return ImportPermitsView(model);
        }

        // cancelEdit (or no command): redisplay the pending list with no row in edit mode.
        return ImportPermitsView(model);
    }

    private ViewResult ImportPermitsView(ImportPermitsViewModel model)
    {
        ViewData[BreadcrumbRouteValues.ContractId] = model.ContractId;
        return View(nameof(ImportPermits), model);
    }

    // Legacy returns to ContractList.aspx from Save and Cancel/Back.
    private async Task<IActionResult> RedirectToContractListAsync(Guid contractId, CancellationToken cancellationToken)
    {
        var contract = await contractApiClient.GetContractAsync(contractId, cancellationToken);
        return contract is null
            ? RedirectToAction(nameof(ManageContracts))
            : RedirectToAction(nameof(Index), new { customerId = contract.CustomerId });
    }

    // Reloads the read-only columns (and Required) from the database, then overlays each row's
    // pending Received/Expiry posted by the form.
    private async Task<ImportPermitsViewModel> BuildPendingImportPermitsModelAsync(Guid contractId, IReadOnlyList<ImportPermitPendingRow> pending, CancellationToken cancellationToken)
    {
        var permits = await importPermitApiClient.GetImportPermitsAsync(contractId, cancellationToken);
        var model = ToImportPermitsViewModel(contractId, permits);
        foreach (var row in model.Permits)
        {
            var pendingRow = pending.LastOrDefault(p => p.ParticipantSchemeId == row.ParticipantSchemeId);
            if (pendingRow is null)
            {
                continue;
            }

            row.ImportPermitReceived = pendingRow.ImportPermitReceived ?? row.ImportPermitReceived;
            row.ImportPermitExpiry = string.IsNullOrWhiteSpace(pendingRow.ImportPermitExpiry) ? null : pendingRow.ImportPermitExpiry.Trim();
        }

        return model;
    }

    // Matches legacy GridViewImportPermits_RowUpdating: the expiry is only kept while the permit is
    // required AND received (otherwise it is cleared), and is required/validated only in that case.
    // Only the in-memory model changes - no database write, no notification.
    private ViewResult ApplyImportPermitRowEdit(ImportPermitsViewModel model, Guid? rowId, bool? editReceived, string? editExpiry)
    {
        var row = model.Permits.FirstOrDefault(p => p.ParticipantSchemeId == rowId);
        if (row is null)
        {
            return ImportPermitsView(model);
        }

        // A disabled Received dropdown (permit not required) is not posted - keep the pending value.
        var received = editReceived ?? row.ImportPermitReceived;
        var expiryKept = row.ImportPermitRequired && received;
        var expiry = expiryKept ? ParseExpiry(editExpiry) : null;

        if (expiryKept && expiry is null)
        {
            ModelState.AddModelError(
                ImportPermitExpiryField,
                string.IsNullOrWhiteSpace(editExpiry) ? "Expiry date required" : "Invalid date. Please enter a valid date in dd/mm/yyyy format.");

            // Redisplay the row still in edit mode with what the user actually typed.
            model.EditParticipantSchemeId = row.ParticipantSchemeId;
            model.EditReceived = received;
            model.EditExpiry = editExpiry;
            return ImportPermitsView(model);
        }

        row.ImportPermitReceived = received;
        row.ImportPermitExpiry = expiry?.ToString(ExpiryFormat, CultureInfo.InvariantCulture);
        return ImportPermitsView(model);
    }

    // Matches legacy btnApply/btnSave: writes every row (not just the changed ones) to the database.
    // Returns false (with an error banner) if any write is rejected.
    private async Task<bool> SaveImportPermitsAsync(ImportPermitsViewModel model, CancellationToken cancellationToken)
    {
        foreach (var row in model.Permits)
        {
            var updated = await importPermitApiClient.UpdateImportPermitAsync(
                row.ParticipantSchemeId,
                new UpdateImportPermitRequest(row.ImportPermitReceived, ParseExpiry(row.ImportPermitExpiry)),
                cancellationToken);
            if (!updated)
            {
                TempData.SetNotification(NotificationType.Error, "The import permits could not be saved.");
                return false;
            }
        }

        TempData.SetNotification(NotificationType.Success, "Import Permit(s) saved successfully.");
        return true;
    }

    // Matches legacy validatePermitExpiry()/cvPermitExpiry_ServerValidate's exact dd/MM/yyyy format.
    private const string ExpiryFormat = "dd/MM/yyyy";

    // ModelState key / input id of the edited row's expiry field.
    private const string ImportPermitExpiryField = "importPermitExpiry";

    private static DateTime? ParseExpiry(string? text) =>
        !string.IsNullOrWhiteSpace(text) &&
        DateTime.TryParseExact(text.Trim(), ExpiryFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private static ImportPermitsViewModel ToImportPermitsViewModel(Guid contractId, IReadOnlyList<ImportPermitResponse> permits) => new()
    {
        ContractId = contractId,
        Permits = permits.Select(p => new ImportPermitRowViewModel
        {
            ParticipantSchemeId = p.ParticipantSchemeId,
            SchemeNumber = p.SchemeNumber,
            SchemeName = p.SchemeName,
            LabId = p.LabId,
            ImportPermitRequired = p.ImportPermitRequired,
            ImportPermitReceived = p.ImportPermitReceived,
            ImportPermitExpiry = p.ImportPermitExpiry?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
        }).ToList()
    };

    // Legacy MergeContracts.aspx - screen users know as "Renew Contracts".
    [HttpGet]
    public async Task<IActionResult> RenewContracts(Guid customerId, CancellationToken cancellationToken)
    {
        var model = await BuildRenewContractsModelAsync(customerId, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenewContracts(
        Guid customerId,
        List<Guid> selectedContractIds,
        List<Guid> selectedParticipantSchemeIds,
        string? newContractSignatory,
        CancellationToken cancellationToken)
    {
        selectedContractIds ??= [];
        selectedParticipantSchemeIds ??= [];

        var request = new RenewContractRequest(selectedContractIds, selectedParticipantSchemeIds, newContractSignatory);
        var result = await contractRenewalApiClient.RenewContractsAsync(customerId, request, cancellationToken);

        if (!result.Success)
        {
            var model = await BuildRenewContractsModelAsync(customerId, cancellationToken);
            model.SelectedContractIds = selectedContractIds;
            model.SelectedParticipantSchemeIds = selectedParticipantSchemeIds;
            model.NewContractSignatory = newContractSignatory;
            model.ErrorMessage = result.ErrorMessage;
            return View(model);
        }

        TempData.SetNotification(NotificationType.Success, "Contract renewed successfully.");
        return RedirectToAction(nameof(Index), new { customerId });
    }

    private async Task<RenewContractsViewModel> BuildRenewContractsModelAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await customerApiClient.GetCustomerAsync(customerId, cancellationToken);
        var contracts = await contractRenewalApiClient.GetRenewableContractsAsync(customerId, cancellationToken);
        var items = contracts.IsAllowed
            ? await contractRenewalApiClient.GetRenewableItemsAsync(customerId, cancellationToken)
            : new RenewableContractItemsResponse([]);

        return new RenewContractsViewModel
        {
            CustomerId = customerId,
            CustomerName = customer?.Name ?? string.Empty,
            CustomerOrganisation = customer?.Organisation ?? string.Empty,
            QalNumber = customer?.QalNumber ?? string.Empty,
            IsAllowed = contracts.IsAllowed,
            BlockedReason = contracts.BlockedReason,
            Contracts = contracts.Contracts,
            Items = items.Items,
            ExistingSignatories = contracts.ExistingSignatories,
            SelectedContractIds = contracts.Contracts.Select(c => c.ContractId).ToList(),
            SelectedParticipantSchemeIds = items.Items.Where(i => i.IsRenewable).Select(i => i.ParticipantSchemeId).ToList()
        };
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var contract = await contractApiClient.GetContractAsync(id, cancellationToken);
        if (contract is null)
        {
            LogContractNotFoundMessage(logger, id, null);
            return NotFound();
        }

        ViewData[BreadcrumbRouteValues.CustomerId] = contract.CustomerId;
        return View(contract);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid customerId, CancellationToken cancellationToken)
    {
        var model = new ContractFormViewModel { CustomerId = customerId, IsActive = true, ContractType = "UT" };
        await PopulateYearOptionsAsync(model, cancellationToken);
        await PopulateCustomerContextAsync(model, customerId, cancellationToken);

        // Suggest the system-wide default UT number (legacy Contract.DataPortal_Create() -
        // SystemObjects.SystemSettings.FetchSystemSettings().UTNumber) - the admin can still
        // overwrite it, or switch the dropdown to FT and enter an FT number instead.
        var systemSettings = await lookupApiClient.GetSystemSettingsAsync(cancellationToken);
        model.UTNumber = systemSettings.UTNumber;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid customerId, ContractFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.CustomerId = customerId;
            await PopulateYearOptionsAsync(model, cancellationToken);
            await PopulateCustomerContextAsync(model, customerId, cancellationToken);
            return View(model);
        }

        var result = await contractApiClient.CreateContractAsync(customerId, ToRequest(model), cancellationToken);
        if (!result.Success)
        {
            LogCreateFailedMessage(logger, customerId, null);
            AddErrors(result.FieldErrors);
            model.CustomerId = customerId;
            await PopulateYearOptionsAsync(model, cancellationToken);
            await PopulateCustomerContextAsync(model, customerId, cancellationToken);
            return View(model);
        }

        ArgumentNullException.ThrowIfNull(result.Contract);
        LogCreatedContractMessage(logger, result.Contract.ContractId, null);
        TempData.SetNotification(NotificationType.Success, "Contract created successfully.");
        return RedirectToAction(nameof(Details), new { id = result.Contract.ContractId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var contract = await contractApiClient.GetContractAsync(id, cancellationToken);
        if (contract is null)
        {
            return NotFound();
        }

        var model = ToFormViewModel(contract);
        await PopulateYearOptionsAsync(model, cancellationToken);
        await PopulateCustomerContextAsync(model, contract.CustomerId, cancellationToken);
        ViewData[BreadcrumbRouteValues.CustomerId] = contract.CustomerId;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ContractFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateYearOptionsAsync(model, cancellationToken);
            await PopulateCustomerContextAsync(model, model.CustomerId.GetValueOrDefault(), cancellationToken);
            return View(model);
        }

        var result = await contractApiClient.UpdateContractAsync(id, ToRequest(model), cancellationToken);
        if (!result.Success)
        {
            LogUpdateFailedMessage(logger, id, null);
            AddErrors(result.FieldErrors);
            await PopulateYearOptionsAsync(model, cancellationToken);
            await PopulateCustomerContextAsync(model, model.CustomerId.GetValueOrDefault(), cancellationToken);
            return View(model);
        }

        LogUpdatedContractMessage(logger, id, null);
        TempData.SetNotification(NotificationType.Success, "Contract updated successfully.");
        return RedirectToAction(nameof(Details), new { id });
    }

    // Review Pending Orders (legacy ReviewPendingOrders.aspx) - two grids, current and next year.
    public async Task<IActionResult> ReviewPendingOrders(CancellationToken cancellationToken)
    {
        var orders = await contractApiClient.GetPendingOrdersAsync(cancellationToken);
        return View(new PendingOrderListViewModel(orders.CurrentYearOrders, orders.NextYearOrders));
    }

    // Pending Order Details (legacy PendingContractOrder.aspx).
    public async Task<IActionResult> PendingOrderDetails(Guid pendingContractId, CancellationToken cancellationToken)
    {
        var order = await contractApiClient.GetPendingOrderAsync(pendingContractId, cancellationToken);
        return order is null ? NotFound() : View(new PendingOrderDetailsViewModel(order));
    }

    // Legacy toggles a month / the Import-Export Licence checkbox with an AutoPostBack that saves
    // the row and re-totals the order; here the whole row posts back at once.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePendingOrderScheme(
        Guid pendingContractId,
        Guid pendingParticipantSchemeId,
        PendingOrderSchemeUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await contractApiClient.UpdatePendingOrderSchemeAsync(pendingContractId, pendingParticipantSchemeId, request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(PendingOrderDetails), new { pendingContractId });
    }

    // Legacy's per-row Add/Remove link - flips fldIsRemoved, leaving every other value intact.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePendingOrderScheme(
        Guid pendingContractId,
        Guid pendingParticipantSchemeId,
        PendingOrderSchemeUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await contractApiClient.UpdatePendingOrderSchemeAsync(
            pendingContractId,
            pendingParticipantSchemeId,
            request with { IsRemoved = !request.IsRemoved },
            cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(PendingOrderDetails), new { pendingContractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApprovePendingOrder(Guid pendingContractId, string? purchaseOrderNumber, CancellationToken cancellationToken)
    {
        var result = await contractApiClient.ApprovePendingOrderAsync(
            pendingContractId, new PendingOrderApproveRequest(purchaseOrderNumber ?? string.Empty), cancellationToken);

        if (result.NotFound)
        {
            return NotFound();
        }

        if (!result.Success)
        {
            var messages = string.Join(" ", result.FieldErrors.SelectMany(e => e.Value));
            TempData.SetNotification(NotificationType.Error, $"Order could not be approved. {messages}");
            return RedirectToAction(nameof(PendingOrderDetails), new { pendingContractId });
        }

        TempData.SetNotification(NotificationType.Success, "Order approved successfully.");
        return RedirectToAction(nameof(ReviewPendingOrders));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclinePendingOrder(Guid pendingContractId, CancellationToken cancellationToken)
    {
        var declined = await contractApiClient.DeclinePendingOrderAsync(pendingContractId, cancellationToken);
        if (!declined)
        {
            return NotFound();
        }

        TempData.SetNotification(NotificationType.Success, "Order declined successfully.");
        return RedirectToAction(nameof(ReviewPendingOrders));
    }

    private void AddErrors(IReadOnlyDictionary<string, string[]> fieldErrors) => this.AddFieldErrors(fieldErrors);

    // Fetches the current+next year reference list once per request - matches legacy
    // Contract.aspx.vb SetYearDropDown() (SystemObjects.YearCollection.FetchYearCollectionCurrent()).
    private async Task PopulateYearOptionsAsync(ContractFormViewModel model, CancellationToken cancellationToken)
    {
        var years = await lookupApiClient.GetCurrentYearsAsync(cancellationToken);
        model.YearOptions = years
            .Select(y => new SelectListItem(y.Year, y.YearId.ToString(CultureInfo.InvariantCulture)))
            .ToList();
    }

    // Displays the customer's QAL number prominently at the top of Create/Edit (matches legacy
    // Contract.aspx.vb Page_Load: LblSubTitle.Text = mCustomer.QalNumber [+ ", " + Year]) and
    // resolves the currency symbol used to label the Postage + packaging pricing fields (matches
    // Contract.aspx.vb LoadLabelNames() using mCurrency.Symbol, from
    // SystemObjects.CurrencyCollection.FetchCurrencyCollectionByCurrencyId(mCustomer.CurrencyId)).
    private async Task PopulateCustomerContextAsync(ContractFormViewModel model, Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await customerApiClient.GetCustomerAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return;
        }

        model.CustomerName = customer.Name;
        model.QalNumber = customer.QalNumber;

        var currencies = await lookupApiClient.GetCurrenciesAsync(cancellationToken);
        model.CurrencySymbol = currencies.FirstOrDefault(c => c.CurrencyId == customer.CurrencyId)?.Symbol ?? string.Empty;
    }

    private static ContractRequest ToRequest(ContractFormViewModel model) => new(
        model.YearId.GetValueOrDefault(),
        model.UTNumber ?? string.Empty,
        model.FTNumber ?? string.Empty,
        model.ContractSignatory ?? string.Empty,
        model.ActionsRequired ?? string.Empty,
        model.RenewalInformation ?? string.Empty,
        model.DiscountRate.GetValueOrDefault(),
        model.AdministrationCharge.GetValueOrDefault(),
        model.NumberCourier.GetValueOrDefault(),
        model.CourierPrice.GetValueOrDefault(),
        model.NumberPostage.GetValueOrDefault(),
        model.PostagePrice.GetValueOrDefault(),
        model.NumberSpecialDelivery.GetValueOrDefault(),
        model.SpecialDeliveryPrice.GetValueOrDefault(),
        model.AcknowledgementPostedDate,
        model.AcknowledgementReturnedDate,
        model.JobSheetPostedDate,
        model.ReasonForClosure ?? string.Empty,
        model.DateOfLeaving,
        model.IsActive,
        model.Suffix ?? string.Empty,
        model.PurchaseOrderNumber ?? string.Empty,
        model.OptOutOfInvoiceGeneration,
        model.IsOnlineOrder);

    private static ContractFormViewModel ToFormViewModel(ContractResponse contract) => new()
    {
        ContractId = contract.ContractId,
        CustomerId = contract.CustomerId,
        CustomerName = contract.CustomerName,
        QalNumber = contract.QalNumber,
        IsReadOnly = contract.IsReadOnly,
        CommencementDate = contract.CommencementDate,
        YearId = contract.YearId,
        UTNumber = contract.UTNumber,
        FTNumber = contract.FTNumber,
        ContractType = string.IsNullOrEmpty(contract.FTNumber) ? "UT" : "FT",
        ContractSignatory = contract.ContractSignatory,
        ActionsRequired = contract.ActionsRequired,
        RenewalInformation = contract.RenewalInformation,
        DiscountRate = contract.DiscountRate,
        AdministrationCharge = contract.AdministrationCharge,
        NumberCourier = contract.NumberCourier,
        CourierPrice = contract.CourierPrice,
        NumberPostage = contract.NumberPostage,
        PostagePrice = contract.PostagePrice,
        NumberSpecialDelivery = contract.NumberSpecialDelivery,
        SpecialDeliveryPrice = contract.SpecialDeliveryPrice,
        AcknowledgementPostedDate = contract.AcknowledgementPostedDate,
        AcknowledgementReturnedDate = contract.AcknowledgementReturnedDate,
        JobSheetPostedDate = contract.JobSheetPostedDate,
        ReasonForClosure = contract.ReasonForClosure,
        DateOfLeaving = contract.DateOfLeaving,
        IsActive = contract.IsActive,
        Suffix = contract.Suffix,
        PurchaseOrderNumber = contract.PurchaseOrderNumber,
        OptOutOfInvoiceGeneration = contract.OptOutOfInvoiceGeneration,
        IsOnlineOrder = contract.IsOnlineOrder,
        IsInvoiceSent = contract.IsInvoiceSent
    };
}
