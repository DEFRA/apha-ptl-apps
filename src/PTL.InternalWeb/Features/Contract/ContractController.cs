using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Features.Contract;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access to Contract functionality. Policies will be added later.
public class ContractController(IContractApiClient contractApiClient, ILookupApiClient lookupApiClient, ILogger<ContractController> logger) : Controller
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
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await contractApiClient.GetContractsForCustomerAsync(customerId, new ContractSearchRequest(yearId, period, searchTerm, page, pageSize), cancellationToken);
        LogDisplayedContractListMessage(logger, customerId, yearId, searchTerm, page, result.TotalCount, null);

        var search = new ContractSearchViewModel(customerId, yearId, period, searchTerm, result.Page, result.PageSize);
        return View(new ContractListViewModel(search, result.TotalCount, result.Items));
    }

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
        var model = new ContractFormViewModel { CustomerId = customerId, IsActive = true };
        await PopulateYearOptionsAsync(model, cancellationToken);
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
            return View(model);
        }

        var result = await contractApiClient.CreateContractAsync(customerId, ToCreateRequest(model), cancellationToken);
        if (!result.Success)
        {
            LogCreateFailedMessage(logger, customerId, null);
            AddErrors(result.FieldErrors);
            model.CustomerId = customerId;
            await PopulateYearOptionsAsync(model, cancellationToken);
            return View(model);
        }

        LogCreatedContractMessage(logger, result.Contract!.ContractId, null);
        return RedirectToAction(nameof(Details), new { id = result.Contract!.ContractId });
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
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ContractFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateYearOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await contractApiClient.UpdateContractAsync(id, ToUpdateRequest(model), cancellationToken);
        if (!result.Success)
        {
            LogUpdateFailedMessage(logger, id, null);
            AddErrors(result.FieldErrors);
            await PopulateYearOptionsAsync(model, cancellationToken);
            return View(model);
        }

        LogUpdatedContractMessage(logger, id, null);
        return RedirectToAction(nameof(Details), new { id });
    }

    private void AddErrors(IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        foreach (var (field, messages) in fieldErrors)
        {
            foreach (var message in messages)
            {
                ModelState.AddModelError(field, message);
            }
        }
    }

    // Fetches the current+next year reference list once per request - matches legacy
    // Contract.aspx.vb SetYearDropDown() (SystemObjects.YearCollection.FetchYearCollectionCurrent()).
    private async Task PopulateYearOptionsAsync(ContractFormViewModel model, CancellationToken cancellationToken)
    {
        var years = await lookupApiClient.GetCurrentYearsAsync(cancellationToken);
        model.YearOptions = years
            .Select(y => new SelectListItem(y.Year, y.YearId.ToString()))
            .ToList();
    }

    private static CreateContractRequest ToCreateRequest(ContractFormViewModel model) => new(
        model.YearId,
        model.UTNumber ?? string.Empty,
        model.FTNumber ?? string.Empty,
        model.ContractSignatory ?? string.Empty,
        model.ActionsRequired ?? string.Empty,
        model.RenewalInformation ?? string.Empty,
        model.DiscountRate,
        model.AdministrationCharge,
        model.NumberCourier,
        model.CourierPrice,
        model.NumberPostage,
        model.PostagePrice,
        model.NumberSpecialDelivery,
        model.SpecialDeliveryPrice,
        model.AcknowledgementPostedDate ?? default,
        model.AcknowledgementReturnedDate ?? default,
        model.JobSheetPostedDate ?? default,
        model.ReasonForClosure ?? string.Empty,
        model.DateOfLeaving ?? default,
        model.IsActive,
        model.Suffix ?? string.Empty,
        model.PurchaseOrderNumber ?? string.Empty,
        model.OptOutOfInvoiceGeneration,
        model.IsOnlineOrder);

    private static UpdateContractRequest ToUpdateRequest(ContractFormViewModel model) => new(
        model.YearId,
        model.UTNumber ?? string.Empty,
        model.FTNumber ?? string.Empty,
        model.ContractSignatory ?? string.Empty,
        model.ActionsRequired ?? string.Empty,
        model.RenewalInformation ?? string.Empty,
        model.DiscountRate,
        model.AdministrationCharge,
        model.NumberCourier,
        model.CourierPrice,
        model.NumberPostage,
        model.PostagePrice,
        model.NumberSpecialDelivery,
        model.SpecialDeliveryPrice,
        model.AcknowledgementPostedDate ?? default,
        model.AcknowledgementReturnedDate ?? default,
        model.JobSheetPostedDate ?? default,
        model.ReasonForClosure ?? string.Empty,
        model.DateOfLeaving ?? default,
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
        IsOnlineOrder = contract.IsOnlineOrder
    };
}
