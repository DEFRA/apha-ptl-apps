using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Contracts.Customer;

namespace PTL.InternalWeb.Features.Customer;

// Authentication/authorization are out of scope for this phase - assume the current user is
// already authenticated with full access to Customer functionality. Policies will be added later.
public class CustomerController(ICustomerApiClient customerApiClient, ILogger<CustomerController> logger) : Controller
{
    public async Task<IActionResult> Index(
        string? searchTerm,
        CustomerStatusFilter status = CustomerStatusFilter.Active,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await customerApiClient.SearchCustomersAsync(new CustomerSearchRequest(searchTerm, status, page, pageSize), cancellationToken);
        logger.LogInformation("Displayed customer list: searchTerm={SearchTerm} status={Status} page={Page} totalResults={TotalCount}", searchTerm, status, page, result.TotalCount);
        return View(new CustomerListViewModel(searchTerm, status, result.Page, result.PageSize, result.TotalCount, result.Items));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customerApiClient.GetCustomerAsync(id, cancellationToken);
        if (customer is null)
        {
            logger.LogInformation("Customer {CustomerId} not found", id);
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
            logger.LogWarning("Create failed for customer {Name}", model.Name);
            AddErrors(result.FieldErrors);
            return View(model);
        }

        logger.LogInformation("Created customer {CustomerId}", result.Customer!.CustomerId);
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
            logger.LogWarning("Update failed for customer {CustomerId}", id);
            AddErrors(result.FieldErrors);
            return View(model);
        }

        logger.LogInformation("Updated customer {CustomerId}", id);
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
            logger.LogWarning("Deactivate failed for customer {CustomerId}", id);
            AddErrors(result.FieldErrors);
            model.CustomerId = id;
            return View(model);
        }

        logger.LogInformation("Deactivated customer {CustomerId}", id);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await customerApiClient.ReactivateCustomerAsync(id, cancellationToken);
        if (!result.Success)
        {
            logger.LogWarning("Reactivate failed for customer {CustomerId}", id);
            return result.Customer is null && result.FieldErrors.ContainsKey(string.Empty)
                ? NotFound()
                : RedirectToAction(nameof(Details), new { id });
        }

        logger.LogInformation("Reactivated customer {CustomerId}", id);
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
