using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Customer;

namespace PTL.InternalWeb.Features.Customer;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access to Customer functionality. Policies will be added later.
public class CustomerController(ICustomerApiClient customerApiClient, ILookupApiClient lookupApiClient, ILogger<CustomerController> logger) : Controller
{
    private static readonly Action<ILogger, string?, CustomerStatusFilter, int, int, Exception?> LogDisplayedCustomerListMessage =
        LoggerMessage.Define<string?, CustomerStatusFilter, int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogDisplayedCustomerListMessage)),
            "Displayed customer list: searchTerm={SearchTerm} status={Status} page={Page} totalResults={TotalCount}");

    private static readonly Action<ILogger, Guid, Exception?> LogCustomerNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(2, nameof(LogCustomerNotFoundMessage)),
            "Customer {CustomerId} not found");

    private static readonly Action<ILogger, string, Exception?> LogCreateFailedMessage =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, nameof(LogCreateFailedMessage)),
            "Create failed for customer {Name}");

    private static readonly Action<ILogger, Guid, Exception?> LogCreatedCustomerMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(4, nameof(LogCreatedCustomerMessage)),
            "Created customer {CustomerId}");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdateFailedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(5, nameof(LogUpdateFailedMessage)),
            "Update failed for customer {CustomerId}");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdatedCustomerMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(6, nameof(LogUpdatedCustomerMessage)),
            "Updated customer {CustomerId}");

    public async Task<IActionResult> Index(
        string? searchTerm,
        CustomerStatusFilter status = CustomerStatusFilter.Active,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await customerApiClient.SearchCustomersAsync(new CustomerSearchRequest(searchTerm, status, page, pageSize), cancellationToken);
        LogDisplayedCustomerListMessage(logger, searchTerm, status, page, result.TotalCount, null);
        return View(new CustomerListViewModel(searchTerm, status, result.Page, result.PageSize, result.TotalCount, result.Items));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customerApiClient.GetCustomerAsync(id, cancellationToken);
        if (customer is null)
        {
            LogCustomerNotFoundMessage(logger, id, null);
            return NotFound();
        }

        return View(customer);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        // InitialStartDate is server-generated at save time (CustomerService.CreateCustomerAsync
        // sets it to DateTime.UtcNow) - shown here only as a "today" preview, matching legacy
        // Customer.aspx's disabled TextboxInitialStartDate.
        var model = new CustomerFormViewModel { InitialStartDate = DateTime.UtcNow };
        await PopulateLookupOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model, CancellationToken cancellationToken)
    {
        // Not posted back (no input renders it) - restore the "today" preview for redisplay.
        model.InitialStartDate = DateTime.UtcNow;

        if (!ModelState.IsValid)
        {
            await PopulateLookupOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await customerApiClient.CreateCustomerAsync(ToRequest(model), cancellationToken);
        if (!result.Success)
        {
            LogCreateFailedMessage(logger, model.Name, null);
            AddErrors(result.FieldErrors);
            await PopulateLookupOptionsAsync(model, cancellationToken);
            return View(model);
        }

        LogCreatedCustomerMessage(logger, result.Customer!.CustomerId, null);
        return RedirectToAction(nameof(Details), new { id = result.Customer!.CustomerId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customerApiClient.GetCustomerAsync(id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        var model = ToFormViewModel(customer);
        await PopulateLookupOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CustomerFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await RestoreDisplayOnlyFieldsAsync(model, id, cancellationToken);
            await PopulateLookupOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await customerApiClient.UpdateCustomerAsync(id, ToRequest(model), cancellationToken);
        if (!result.Success)
        {
            LogUpdateFailedMessage(logger, id, null);
            AddErrors(result.FieldErrors);
            await RestoreDisplayOnlyFieldsAsync(model, id, cancellationToken);
            await PopulateLookupOptionsAsync(model, cancellationToken);
            return View(model);
        }

        LogUpdatedCustomerMessage(logger, id, null);
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

    // Fetches the Country/Currency/CustomerType/VatRating reference lists once per request and
    // shapes them into SelectListItem so _CustomerForm.cshtml can render <select asp-items="..."> -
    // matches the legacy DropDownCountry/DropDownCurrency DataBind() calls in Customer.aspx.vb
    // LoadLabelNames() (VatRating has no legacy dropdown to mirror, added per explicit request).
    private async Task PopulateLookupOptionsAsync(CustomerFormViewModel model, CancellationToken cancellationToken)
    {
        var countriesTask = lookupApiClient.GetCountriesAsync(cancellationToken);
        var currenciesTask = lookupApiClient.GetCurrenciesAsync(cancellationToken);
        var customerTypesTask = lookupApiClient.GetCustomerTypesAsync(cancellationToken);
        var vatRatingsTask = lookupApiClient.GetVatRatingsAsync(cancellationToken);
        await Task.WhenAll(countriesTask, currenciesTask, customerTypesTask, vatRatingsTask);

        // Country is optional-until-active in CustomerValidator, so a blank option is offered -
        // matches DropDownCountry.Items.Insert(0, New ListItem("- Please Select -", Guid.Empty)).
        var countryOptions = countriesTask.Result
            .Select(c => new SelectListItem(c.Country, c.CountryId.ToString()))
            .Prepend(new SelectListItem("- Please Select -", Guid.Empty.ToString()))
            .ToList();
        model.CountryOptions = countryOptions;
        model.InvoiceCountryOptions = countryOptions;

        // Currency has no blank option in the legacy DropDownCurrency (it is never required to be
        // empty), so none is added here either.
        model.CurrencyOptions = currenciesTask.Result
            .Select(c => new SelectListItem(c.LongName, c.CurrencyId.ToString()))
            .ToList();

        // CustomerTypeId is validated as required (non-empty GUID) by both CustomerValidator and
        // the legacy ValidCustomerType rule, so a blank option is offered to force an explicit choice.
        model.CustomerTypeOptions = customerTypesTask.Result
            .Select(c => new SelectListItem(c.CustomerType, c.CustomerTypeId.ToString()))
            .Prepend(new SelectListItem("- Please Select -", Guid.Empty.ToString()))
            .ToList();

        // VatRatingId is not validated as required anywhere, so a blank option is offered.
        model.VatRatingOptions = vatRatingsTask.Result
            .Select(v => new SelectListItem(v.VatRating, v.VatRatingId.ToString()))
            .Prepend(new SelectListItem("- Please Select -", Guid.Empty.ToString()))
            .ToList();
    }

    private static CustomerSaveRequest ToRequest(CustomerFormViewModel model) => new(
        model.RegisteredFileNumber ?? string.Empty,
        model.Name,
        model.PreviousName ?? string.Empty,
        model.CustomerTypeId,
        model.VatNumber ?? string.Empty,
        model.VatRatingId,
        model.AccountNumber ?? string.Empty,
        model.CustomerFinanceId ?? string.Empty,
        model.ContactName ?? string.Empty,
        model.Organisation ?? string.Empty,
        model.Address1 ?? string.Empty,
        model.Address2 ?? string.Empty,
        model.Address3 ?? string.Empty,
        model.Address4 ?? string.Empty,
        model.Address5 ?? string.Empty,
        model.CountryId,
        model.Telephone ?? string.Empty,
        model.Telephone2 ?? string.Empty,
        model.Fax ?? string.Empty,
        model.Email ?? string.Empty,
        model.CurrencyId,
        model.Comments ?? string.Empty,
        model.PostageArrangements ?? string.Empty,
        model.PaymentNonUK,
        model.InvoiceName ?? string.Empty,
        model.InvoiceOrganisation ?? string.Empty,
        model.InvoiceAddress1 ?? string.Empty,
        model.InvoiceAddress2 ?? string.Empty,
        model.InvoiceAddress3 ?? string.Empty,
        model.InvoiceAddress4 ?? string.Empty,
        model.InvoiceAddress5 ?? string.Empty,
        model.InvoiceCountryId,
        model.InvoiceTelephone ?? string.Empty,
        model.InvoiceTelephone2 ?? string.Empty,
        model.InvoiceFax ?? string.Empty,
        model.InvoiceEmail ?? string.Empty,
        model.IsActive,
        model.CanOrderOnline,
        model.CustomerStatusId);

    private static CustomerFormViewModel ToFormViewModel(CustomerResponse customer) => new()
    {
        CustomerId = customer.CustomerId,
        QalNumber = customer.QalNumber,
        InitialStartDate = customer.InitialStartDate,
        RegisteredFileNumber = customer.RegisteredFileNumber,
        Name = customer.Name,
        PreviousName = customer.PreviousName,
        CustomerTypeId = customer.CustomerTypeId,
        VatNumber = customer.VatNumber,
        VatRatingId = customer.VatRatingId,
        AccountNumber = customer.AccountNumber,
        CustomerFinanceId = customer.CustomerFinanceId,
        ContactName = customer.ContactName,
        Organisation = customer.Organisation,
        Address1 = customer.Address1,
        Address2 = customer.Address2,
        Address3 = customer.Address3,
        Address4 = customer.Address4,
        Address5 = customer.Address5,
        CountryId = customer.CountryId,
        Telephone = customer.Telephone,
        Telephone2 = customer.Telephone2,
        Fax = customer.Fax,
        Email = customer.Email,
        CurrencyId = customer.CurrencyId,
        Comments = customer.Comments,
        PostageArrangements = customer.PostageArrangements,
        PaymentNonUK = customer.PaymentNonUK,
        InvoiceName = customer.InvoiceName,
        InvoiceOrganisation = customer.InvoiceOrganisation,
        InvoiceAddress1 = customer.InvoiceAddress1,
        InvoiceAddress2 = customer.InvoiceAddress2,
        InvoiceAddress3 = customer.InvoiceAddress3,
        InvoiceAddress4 = customer.InvoiceAddress4,
        InvoiceAddress5 = customer.InvoiceAddress5,
        InvoiceCountryId = customer.InvoiceCountryId,
        InvoiceTelephone = customer.InvoiceTelephone,
        InvoiceTelephone2 = customer.InvoiceTelephone2,
        InvoiceFax = customer.InvoiceFax,
        InvoiceEmail = customer.InvoiceEmail,
        IsActive = customer.IsActive,
        CanOrderOnline = customer.CanOrderOnline,
        InactiveDate = customer.InactiveDate,
        CustomerStatusId = customer.CustomerStatusId
    };

    // QalNumber/CustomerId/InitialStartDate/InactiveDate are display-only (no form input renders
    // them), so a posted-back model on a failed Edit submission has them blank/default - re-fetch
    // the persisted customer to restore them for redisplay.
    private async Task RestoreDisplayOnlyFieldsAsync(CustomerFormViewModel model, Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await customerApiClient.GetCustomerAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return;
        }

        model.CustomerId = customer.CustomerId;
        model.QalNumber = customer.QalNumber;
        model.InitialStartDate = customer.InitialStartDate;
        model.InactiveDate = customer.InactiveDate;
    }
}
