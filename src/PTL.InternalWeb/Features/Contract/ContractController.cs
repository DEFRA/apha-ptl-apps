using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Features.Contract;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access to Contract functionality. Policies will be added later.
public class ContractController(IContractApiClient contractApiClient, ICustomerApiClient customerApiClient, ILookupApiClient lookupApiClient, ILogger<ContractController> logger) : Controller
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

    // Legacy ContractItems.aspx (priced scheme line items) has not been migrated yet - see
    // docs/migration/contract-migration.md "Feature Breakdown" Phase 1/3. Stub keeps the Contract
    // list's column/link parity without reimplementing that separate, larger feature.
    public IActionResult ContractItems(Guid id) => View("FeatureNotAvailable", "Contract items");

    // Legacy mail-merge export (Contract/Address Confirmation/Job Sheet/Renewal Letter/Import
    // Permit(s)) depends on template-upload infrastructure explicitly deferred to a later phase
    // per docs/migration/contract-migration.md. Stub keeps the Export column's link parity.
    public IActionResult Export(Guid id, string documentType) => View("FeatureNotAvailable", documentType);

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
