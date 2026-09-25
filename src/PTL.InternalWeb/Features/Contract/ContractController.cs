using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Features.Contract;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access to Contract functionality. Policies will be added later.
public class ContractController(IContractApiClient contractApiClient, ICustomerApiClient customerApiClient, ILookupApiClient lookupApiClient, IImportPermitApiClient importPermitApiClient, ILogger<ContractController> logger) : Controller
{
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

    public async Task<IActionResult> Index(
        Guid customerId,
        int? yearId,
        ContractPeriodFilter period = ContractPeriodFilter.CurrentAndNext,
        string? searchTerm = null,
        int page = 1,
        int pageSize = PTL.InternalWeb.Pagination.PaginationModel.DefaultPageSize,
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
        LogDisplayedContractItemsMessage(logger, id, items.Schemes.Count, null);
        return View(model);
    }

    // Explicit REST mutation replacing legacy ContractItems.aspx's deferred "mark removed, save
    // later" flow - removes immediately, then redisplays the Contract Items page.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveContractItem(Guid id, Guid participantSchemeId, CancellationToken cancellationToken)
    {
        var result = await contractApiClient.RemoveContractItemAsync(id, participantSchemeId, cancellationToken);
        if (!result.Success)
        {
            LogRemoveContractItemFailedMessage(logger, id, participantSchemeId, result.ErrorMessage, null);
            TempData["ContractItemsError"] = result.ErrorMessage ?? "The contract item could not be removed.";
        }
        else
        {
            LogRemovedContractItemMessage(logger, id, participantSchemeId, null);
        }

        return RedirectToAction(nameof(ContractItems), new { id });
    }

    // Legacy mail-merge export (Contract/Address Confirmation/Job Sheet/Renewal Letter) depends on
    // template-upload infrastructure explicitly deferred to a later phase per docs/migration/
    // contract-migration.md. Stub keeps the Export column's link parity. Import Permit(s) is NOT a
    // document export - see the dedicated ImportPermits action below.
    public IActionResult Export(Guid id, string documentType) => View("FeatureNotAvailable", documentType);

    // Legacy ImportPermit.aspx GridViewImportPermits: a CommandField "Actions" column with an Edit
    // button per row; clicking it switches that row into edit mode (Update/Cancel). Only the
    // "Import Permit Received"/"Import Permit Expiry" cells have an EditItemTemplate - "Import
    // Permit Required" has none, so it stays the same read-only, disabled checkbox in both view and
    // edit mode (it is never itself editable - see ImportPermitViewModel.cs). editParticipantSchemeId
    // carries which single row is in edit mode across the GET, replacing WebForms' GridView.EditIndex
    // ViewState.
    [HttpGet]
    public async Task<IActionResult> ImportPermits(Guid id, Guid? editParticipantSchemeId, CancellationToken cancellationToken)
    {
        var permits = await importPermitApiClient.GetImportPermitsAsync(id, cancellationToken);
        LogDisplayedImportPermitsMessage(logger, id, permits.Count, null);
        var model = ToImportPermitsViewModel(id, permits);
        model.EditParticipantSchemeId = editParticipantSchemeId;
        return View(model);
    }

    // Matches legacy GridViewImportPermits_RowUpdating - commits the single edited row and returns
    // to view mode (EditIndex = -1). Received/Expiry are posted as individual values (not the whole
    // list) since only one row is ever editable at a time.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateImportPermit(Guid id, Guid participantSchemeId, bool importPermitReceived, string? importPermitExpiry, CancellationToken cancellationToken)
    {
        var expiry = ParseExpiry(importPermitExpiry);
        if (importPermitReceived && expiry is null)
        {
            ModelState.AddModelError(
                nameof(importPermitExpiry),
                string.IsNullOrWhiteSpace(importPermitExpiry) ? "Expiry date required" : "Invalid date. Please enter a valid date in dd/mm/yyyy format.");

            var permits = await importPermitApiClient.GetImportPermitsAsync(id, cancellationToken);
            var model = ToImportPermitsViewModel(id, permits);
            model.EditParticipantSchemeId = participantSchemeId;

            // Redisplay the row still in edit mode with what the user actually typed, not the
            // last-saved value, matching legacy redisplaying the postback's own EditItemTemplate values.
            var row = model.Permits.FirstOrDefault(p => p.ParticipantSchemeId == participantSchemeId);
            if (row is not null)
            {
                row.ImportPermitReceived = importPermitReceived;
                row.ImportPermitExpiry = importPermitExpiry;
            }

            return View(nameof(ImportPermits), model);
        }

        await importPermitApiClient.UpdateImportPermitAsync(participantSchemeId, new UpdateImportPermitRequest(importPermitReceived, expiry), cancellationToken);
        return RedirectToAction(nameof(ImportPermits), new { id });
    }

    // Matches legacy validatePermitExpiry()/cvPermitExpiry_ServerValidate's exact dd/MM/yyyy format.
    private static DateTime? ParseExpiry(string? text) =>
        !string.IsNullOrWhiteSpace(text) &&
        DateTime.TryParseExact(text.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
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
            ImportPermitExpiry = p.ImportPermitExpiry?.ToString("dd/MM/yyyy")
        }).ToList()
    };

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var contract = await contractApiClient.GetContractAsync(id, cancellationToken);
        if (contract is null)
        {
            LogContractNotFoundMessage(logger, id, null);
            return NotFound();
        }

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
        return RedirectToAction(nameof(Details), new { id });
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
