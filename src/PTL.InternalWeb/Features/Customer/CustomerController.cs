using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Customer;

namespace PTL.InternalWeb.Features.Customer;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access to Customer functionality. Policies will be added later.
public class CustomerController(ICustomerApiClient customerApiClient, ILogger<CustomerController> logger) : Controller
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

    private static readonly Action<ILogger, Guid, Exception?> LogDeactivateFailedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(7, nameof(LogDeactivateFailedMessage)),
            "Deactivate failed for customer {CustomerId}");

    private static readonly Action<ILogger, Guid, Exception?> LogDeactivatedCustomerMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(8, nameof(LogDeactivatedCustomerMessage)),
            "Deactivated customer {CustomerId}");

    private static readonly Action<ILogger, Guid, Exception?> LogReactivateFailedMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(9, nameof(LogReactivateFailedMessage)),
            "Reactivate failed for customer {CustomerId}");

    private static readonly Action<ILogger, Guid, Exception?> LogReactivatedCustomerMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(10, nameof(LogReactivatedCustomerMessage)),
            "Reactivated customer {CustomerId}");

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
    public IActionResult Create()
    {
        return View(new CustomerFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await customerApiClient.CreateCustomerAsync(ToCreateRequest(model), cancellationToken);
        if (!result.Success)
        {
            LogCreateFailedMessage(logger, model.Name, null);
            AddErrors(result.FieldErrors);
            return View(model);
        }

        LogCreatedCustomerMessage(logger, result.Customer!.CustomerId, null);
        return RedirectToAction(nameof(Details), new { id = result.Customer!.CustomerId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customerApiClient.GetCustomerAsync(id, cancellationToken);
        return customer is null ? NotFound() : View(ToFormViewModel(customer));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CustomerFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await customerApiClient.UpdateCustomerAsync(id, ToUpdateRequest(model), cancellationToken);
        if (!result.Success)
        {
            LogUpdateFailedMessage(logger, id, null);
            AddErrors(result.FieldErrors);
            return View(model);
        }

        LogUpdatedCustomerMessage(logger, id, null);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customerApiClient.GetCustomerAsync(id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        return View(new DeactivateCustomerViewModel { CustomerId = customer.CustomerId, Name = customer.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id, DeactivateCustomerViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.CustomerId = id;
            return View(model);
        }

        var result = await customerApiClient.DeactivateCustomerAsync(id, model.CustomerStatusId!.Value, cancellationToken);
        if (!result.Success)
        {
            LogDeactivateFailedMessage(logger, id, null);
            AddErrors(result.FieldErrors);
            model.CustomerId = id;
            return View(model);
        }

        LogDeactivatedCustomerMessage(logger, id, null);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await customerApiClient.ReactivateCustomerAsync(id, cancellationToken);
        if (!result.Success)
        {
            LogReactivateFailedMessage(logger, id, null);
            return result.Customer is null && result.FieldErrors.ContainsKey(string.Empty)
                ? NotFound()
                : RedirectToAction(nameof(Details), new { id });
        }

        LogReactivatedCustomerMessage(logger, id, null);
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

    private static CreateCustomerRequest ToCreateRequest(CustomerFormViewModel model) => new(
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

    private static UpdateCustomerRequest ToUpdateRequest(CustomerFormViewModel model) => new(
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
        CustomerStatusId = customer.CustomerStatusId
    };
}
