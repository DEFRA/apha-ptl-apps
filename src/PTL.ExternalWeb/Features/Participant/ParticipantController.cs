using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ApiClient;
using PTL.Contracts.Participant;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;
using PTL.SharedUI.Notifications;

namespace PTL.ExternalWeb.Features.Participant;

// Legacy EditParticipantDetails.aspx (tracker row 7) - participant self-service edit of
// participant/laboratory details, submitted as a pending update for admin approval (Story 744639).
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class ParticipantController(IParticipantDetailsEditApiClient participantDetailsEditApiClient, ILookupApiClient lookupApiClient) : Controller
{
    private const string PleaseSelectOptionText = "Please select";

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return NotFound();
        }

        var details = await participantDetailsEditApiClient.GetAsync(participantId.Value, cancellationToken);
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
    public async Task<IActionResult> Index(ParticipantDetailsEditViewModel model, CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return NotFound();
        }

        var result = await participantDetailsEditApiClient.SubmitAsync(participantId.Value, ToRequest(model), cancellationToken);
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

    private async Task PopulateCountryOptionsAsync(ParticipantDetailsEditViewModel model, CancellationToken cancellationToken)
    {
        var countries = await lookupApiClient.GetCountriesAsync(cancellationToken);

        // Country is required - a blank option forces an explicit choice, matching legacy's
        // DropDownCountry "- Please Select -" placeholder.
        model.CountryOptions = countries
            .Select(c => new SelectListItem(c.Country, c.CountryId.ToString()))
            .Prepend(new SelectListItem(PleaseSelectOptionText, Guid.Empty.ToString()))
            .ToList();
    }

    private static ParticipantDetailsEditViewModel ToViewModel(ParticipantDetailsEditResponse details) => new()
    {
        LabCode = details.LabCode,
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
        Email = details.Email,
        Email2 = details.Email2
    };

    private static ParticipantDetailsEditSaveRequest ToRequest(ParticipantDetailsEditViewModel model) => new(
        model.ContactName ?? string.Empty,
        model.Organisation ?? string.Empty,
        model.Address1 ?? string.Empty,
        model.Address2 ?? string.Empty,
        model.Address3 ?? string.Empty,
        model.Address4 ?? string.Empty,
        model.Address5 ?? string.Empty,
        model.CountryId.GetValueOrDefault(),
        model.Telephone ?? string.Empty,
        model.Email ?? string.Empty,
        model.Email2 ?? string.Empty);
}
