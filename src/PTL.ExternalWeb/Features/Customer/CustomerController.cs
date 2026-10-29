using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient;
using PTL.Contracts.Customer;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;
using PTL.SharedUI.Notifications;

namespace PTL.ExternalWeb.Features.Customer;

// Legacy EditCustomerDetails.aspx (tracker row 6) - participant self-service edit of
// customer/invoice details, submitted as a pending update for admin approval (Story 744638).
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class CustomerController(ICustomerDetailsEditApiClient customerDetailsEditApiClient, ILookupApiClient lookupApiClient) : Controller
{
    private const string PleaseSelectOptionText = "Please select";

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return NotFound();
        }

        var details = await customerDetailsEditApiClient.GetAsync(participantId.Value, cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        var model = ToViewModel(details);
        await PopulateCountryOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CustomerDetailsEditViewModel model, CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return NotFound();
        }

        var result = await customerDetailsEditApiClient.SubmitAsync(participantId.Value, ToRequest(model), cancellationToken);
        if (!result.Success)
        {
            AddErrors(result.Errors);
            await PopulateCountryOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData.SetNotification(NotificationType.Success, "Your changes have been submitted for approval.");
        return RedirectToAction(nameof(Index));
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

    private async Task PopulateCountryOptionsAsync(CustomerDetailsEditViewModel model, CancellationToken cancellationToken)
    {
        var countries = await lookupApiClient.GetCountriesAsync(cancellationToken);

        // Country/Invoice Country are required - a blank option forces an explicit choice,
        // matching legacy's DropDownCountry "- Please Select -" placeholder.
        var options = countries
            .Select(c => new SelectListItem(c.Country, c.CountryId.ToString()))
            .Prepend(new SelectListItem(PleaseSelectOptionText, Guid.Empty.ToString()))
            .ToList();
        model.CountryOptions = options;
        model.InvoiceCountryOptions = options;
    }

    private static CustomerDetailsEditViewModel ToViewModel(CustomerDetailsEditResponse details) => new()
    {
        QalNumber = details.QalNumber,
        HasOutstandingUpdate = details.HasOutstandingUpdate,
        ContactName = details.ContactName,
        Organisation = details.Organisation,
        Address1 = details.Address1,
        Address2 = details.Address2,
        Address3 = details.Address3,
        Address4 = details.Address4,
        Address5 = details.Address5,
        CountryId = details.CountryId,
        Telephone = details.Telephone,
        Telephone2 = details.Telephone2,
        Email = details.Email,
        InvoiceName = details.InvoiceName,
        InvoiceOrganisation = details.InvoiceOrganisation,
        InvoiceAddress1 = details.InvoiceAddress1,
        InvoiceAddress2 = details.InvoiceAddress2,
        InvoiceAddress3 = details.InvoiceAddress3,
        InvoiceAddress4 = details.InvoiceAddress4,
        InvoiceAddress5 = details.InvoiceAddress5,
        InvoiceCountryId = details.InvoiceCountryId,
        InvoiceTelephone = details.InvoiceTelephone,
        InvoiceTelephone2 = details.InvoiceTelephone2,
        InvoiceEmail = details.InvoiceEmail
    };

    private static CustomerDetailsEditSaveRequest ToRequest(CustomerDetailsEditViewModel model) => new(
        model.ContactName ?? string.Empty,
        model.Organisation ?? string.Empty,
        model.Address1 ?? string.Empty,
        model.Address2 ?? string.Empty,
        model.Address3 ?? string.Empty,
        model.Address4 ?? string.Empty,
        model.Address5 ?? string.Empty,
        model.CountryId.GetValueOrDefault(),
        model.Telephone ?? string.Empty,
        model.Telephone2 ?? string.Empty,
        model.Email ?? string.Empty,
        model.InvoiceName ?? string.Empty,
        model.InvoiceOrganisation ?? string.Empty,
        model.InvoiceAddress1 ?? string.Empty,
        model.InvoiceAddress2 ?? string.Empty,
        model.InvoiceAddress3 ?? string.Empty,
        model.InvoiceAddress4 ?? string.Empty,
        model.InvoiceAddress5 ?? string.Empty,
        model.InvoiceCountryId.GetValueOrDefault(),
        model.InvoiceTelephone ?? string.Empty,
        model.InvoiceTelephone2 ?? string.Empty,
        model.InvoiceEmail ?? string.Empty);
}
