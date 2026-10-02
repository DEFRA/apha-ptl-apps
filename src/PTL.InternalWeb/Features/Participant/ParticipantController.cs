using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Participant;
using PTL.InternalWeb.Notifications;

namespace PTL.InternalWeb.Features.Participant;

public class ParticipantController(IParticipantApiClient participantApiClient, ICustomerApiClient customerApiClient, ILookupApiClient lookupApiClient, ILogger<ParticipantController> logger) : Controller
{
    private static readonly Action<ILogger, string?, bool, int, int, Exception?> LogDisplayedParticipantListMessage =
        LoggerMessage.Define<string?, bool, int, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogDisplayedParticipantListMessage)),
            "Displayed participant list: searchTerm={SearchTerm} includeInactive={IncludeInactive} page={Page} totalResults={TotalCount}");

    private static readonly Action<ILogger, Guid, Exception?> LogParticipantNotFoundMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(2, nameof(LogParticipantNotFoundMessage)),
            "Participant {ParticipantId} not found");

    private static readonly Action<ILogger, Guid, Exception?> LogFailedToCreateParticipantMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(3, nameof(LogFailedToCreateParticipantMessage)),
            "Failed to create participant for customer {CustomerId}");

    private static readonly Action<ILogger, Guid, Exception?> LogFailedToUpdateParticipantMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(4, nameof(LogFailedToUpdateParticipantMessage)),
            "Failed to update participant {ParticipantId}");

    private static readonly Action<ILogger, Guid, Exception?> LogFailedToDeactivateParticipantMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(5, nameof(LogFailedToDeactivateParticipantMessage)),
            "Failed to deactivate participant {ParticipantId}");

    private static readonly Action<ILogger, Guid, Exception?> LogFailedToReactivateParticipantMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(6, nameof(LogFailedToReactivateParticipantMessage)),
            "Failed to reactivate participant {ParticipantId}");

    public async Task<IActionResult> Index(Guid customerId, string? searchTerm = null, bool includeInactive = false, int page = 1, int pageSize = PTL.InternalWeb.Pagination.PaginationModel.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await participantApiClient.SearchParticipantsAsync(new ParticipantSearchRequest(customerId, searchTerm, includeInactive, page, pageSize), cancellationToken);
        LogDisplayedParticipantListMessage(logger, searchTerm, includeInactive, page, result.TotalCount, null);
        return View(new ParticipantListViewModel(customerId, searchTerm, includeInactive, page, pageSize, result.TotalCount, result.Items));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var participant = await participantApiClient.GetParticipantAsync(id, cancellationToken);
        if (participant is null)
        {
            LogParticipantNotFoundMessage(logger, id, null);
            return NotFound();
        }

        var labTypesTask = lookupApiClient.GetLabTypesAsync(cancellationToken);
        var countriesTask = lookupApiClient.GetCountriesAsync(cancellationToken);
        await Task.WhenAll(labTypesTask, countriesTask);

        var labTypeName = labTypesTask.Result.FirstOrDefault(t => t.LabTypeId == participant.LabTypeId)?.Name ?? string.Empty;
        var countryName = countriesTask.Result.FirstOrDefault(c => c.CountryId == participant.CountryId)?.Country ?? string.Empty;

        var model = new ParticipantDetailsViewModel(participant, labTypeName, countryName);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid customerId, CancellationToken cancellationToken)
    {
        var model = new ParticipantFormViewModel
        {
            CustomerId = customerId,
            LabCode = await GenerateLabCodeAsync(customerId, cancellationToken)
        };
        await PopulateLookupOptionsAsync(model, cancellationToken);
        await PopulateCustomerContactAsync(model, customerId, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid customerId, ParticipantFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.CustomerId = customerId;
            if (string.IsNullOrWhiteSpace(model.LabCode))
            {
                model.LabCode = await GenerateLabCodeAsync(customerId, cancellationToken);
            }
            await PopulateLookupOptionsAsync(model, cancellationToken);
            await PopulateCustomerContactAsync(model, customerId, cancellationToken);
            return View(model);
        }

        var result = await participantApiClient.CreateParticipantAsync(new ParticipantRequest(
            customerId,
            model.SsoId.GetValueOrDefault(),
            model.LabCode ?? string.Empty,
            model.LabName ?? string.Empty,
            model.LabTypeId.GetValueOrDefault(),
            model.ContactName ?? string.Empty,
            model.Organisation ?? string.Empty,
            model.Address1 ?? string.Empty,
            model.Address2 ?? string.Empty,
            model.Address3 ?? string.Empty,
            model.Address4 ?? string.Empty,
            model.Address5 ?? string.Empty,
            model.CountryId.GetValueOrDefault(),
            model.Telephone ?? string.Empty,
            model.Fax ?? string.Empty,
            model.Email ?? string.Empty,
            model.Email2 ?? string.Empty,
            model.Comments ?? string.Empty,
            model.IsActive), cancellationToken);

        if (!result.Success)
        {
            LogFailedToCreateParticipantMessage(logger, customerId, null);
            AddErrors(result.FieldErrors);
            model.CustomerId = customerId;
            await PopulateLookupOptionsAsync(model, cancellationToken);
            await PopulateCustomerContactAsync(model, customerId, cancellationToken);
            return View(model);
        }

        TempData.SetNotification(NotificationType.Success, "Participant created successfully.");
        return RedirectToAction(nameof(Details), new { id = result.Participant.ParticipantId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var participant = await participantApiClient.GetParticipantAsync(id, cancellationToken);
        if (participant is null)
        {
            return NotFound();
        }

        var model = ToFormViewModel(participant);
        await PopulateLookupOptionsAsync(model, cancellationToken);
        await PopulateCustomerContactAsync(model, participant.CustomerId, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ParticipantFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await RestoreDisplayOnlyFieldsAsync(model, id, cancellationToken);
            await PopulateLookupOptionsAsync(model, cancellationToken);
            await PopulateCustomerContactAsync(model, model.CustomerId.GetValueOrDefault(), cancellationToken);
            return View(model);
        }

        var result = await participantApiClient.UpdateParticipantAsync(id, new ParticipantRequest(
            model.CustomerId.GetValueOrDefault(),
            model.SsoId.GetValueOrDefault(),
            model.LabCode ?? string.Empty,
            model.LabName ?? string.Empty,
            model.LabTypeId.GetValueOrDefault(),
            model.ContactName ?? string.Empty,
            model.Organisation ?? string.Empty,
            model.Address1 ?? string.Empty,
            model.Address2 ?? string.Empty,
            model.Address3 ?? string.Empty,
            model.Address4 ?? string.Empty,
            model.Address5 ?? string.Empty,
            model.CountryId.GetValueOrDefault(),
            model.Telephone ?? string.Empty,
            model.Fax ?? string.Empty,
            model.Email ?? string.Empty,
            model.Email2 ?? string.Empty,
            model.Comments ?? string.Empty,
            model.IsActive), cancellationToken);

        if (!result.Success)
        {
            // If the participant was not found (no validation errors), return NotFound
            if (result.Participant is null && result.FieldErrors.Count == 0)
            {
                return NotFound();
            }

            LogFailedToUpdateParticipantMessage(logger, id, null);
            AddErrors(result.FieldErrors);
            await RestoreDisplayOnlyFieldsAsync(model, id, cancellationToken);
            await PopulateLookupOptionsAsync(model, cancellationToken);
            await PopulateCustomerContactAsync(model, model.CustomerId.GetValueOrDefault(), cancellationToken);
            return View(model);
        }

        TempData.SetNotification(NotificationType.Success, "Participant updated successfully.");
        return RedirectToAction(nameof(Details), new { id = result.Participant.ParticipantId });
    }

    // Review Pending Participant Updates (legacy ReviewPendingParticipantUpdates.aspx).
    public async Task<IActionResult> ReviewPendingParticipantUpdates(CancellationToken cancellationToken)
    {
        var updates = await participantApiClient.GetPendingParticipantUpdatesAsync(cancellationToken);
        return View(new PendingParticipantUpdateListViewModel(updates));
    }

    // Pending Participant Update Details (legacy PendingParticipantUpdateDetails.aspx) - current vs
    // pending comparison, Approve/Decline/Cancel.
    public async Task<IActionResult> PendingParticipantUpdateDetails(Guid participantId, CancellationToken cancellationToken)
    {
        var comparison = await participantApiClient.GetPendingParticipantUpdateAsync(participantId, cancellationToken);
        if (comparison is null)
        {
            return NotFound();
        }

        var countries = await lookupApiClient.GetCountriesAsync(cancellationToken);
        var countryNames = countries.ToDictionary(c => c.CountryId, c => c.Country);
        return View(BuildPendingParticipantUpdateDetailsViewModel(comparison, countryNames));
    }

    // Edit Pending Participant Update (legacy PendingParticipantUpdateDetails.aspx's editable form).
    [HttpGet]
    public async Task<IActionResult> EditPendingParticipantUpdate(Guid participantId, CancellationToken cancellationToken)
    {
        var comparison = await participantApiClient.GetPendingParticipantUpdateAsync(participantId, cancellationToken);
        if (comparison is null)
        {
            return NotFound();
        }

        var model = ToPendingFormViewModel(comparison);
        await PopulatePendingCountryOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPendingParticipantUpdate(Guid participantId, PendingParticipantUpdateFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulatePendingCountryOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await participantApiClient.ApprovePendingParticipantUpdateAsync(participantId, ToPendingSaveRequest(model), cancellationToken);
        if (result.NotFound)
        {
            return NotFound();
        }

        if (!result.Success)
        {
            AddErrors(result.FieldErrors);
            await PopulatePendingCountryOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData.SetNotification(NotificationType.Success, "Participant update approved successfully.");
        return RedirectToAction(nameof(ReviewPendingParticipantUpdates));
    }

    // Only approved changes update the live participant record - declined changes do not.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApprovePendingParticipantUpdate(Guid participantId, CancellationToken cancellationToken)
    {
        var result = await participantApiClient.ApprovePendingParticipantUpdateAsync(participantId, request: null, cancellationToken);
        if (result.NotFound)
        {
            return NotFound();
        }

        if (!result.Success)
        {
            // The stored pending values break a participant business rule - the reviewer must amend
            // them on the Edit page before the update can be approved.
            var messages = string.Join(" ", result.FieldErrors.SelectMany(e => e.Value));
            TempData.SetNotification(NotificationType.Error, $"Participant update could not be approved. {messages}");
            return RedirectToAction(nameof(EditPendingParticipantUpdate), new { participantId });
        }

        TempData.SetNotification(NotificationType.Success, "Participant update approved successfully.");
        return RedirectToAction(nameof(ReviewPendingParticipantUpdates));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclinePendingParticipantUpdate(Guid participantId, CancellationToken cancellationToken)
    {
        var declined = await participantApiClient.DeclinePendingParticipantUpdateAsync(participantId, cancellationToken);
        if (!declined)
        {
            return NotFound();
        }

        TempData.SetNotification(NotificationType.Success, "Participant update declined successfully.");
        return RedirectToAction(nameof(ReviewPendingParticipantUpdates));
    }

    // Legacy ParticipantViewers.aspx.
    [HttpGet]
    public async Task<IActionResult> Viewers(Guid participantId, CancellationToken cancellationToken)
    {
        var assignment = await participantApiClient.GetParticipantViewersAsync(participantId, cancellationToken);
        if (assignment is null)
        {
            LogParticipantNotFoundMessage(logger, participantId, null);
            return NotFound();
        }

        return View(new ParticipantViewerFormViewModel(
            participantId, assignment.CustomerId, assignment.LabCode, assignment.LabName, assignment.IsActive,
            assignment.AvailableViewers, assignment.AssignedViewers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Viewers(Guid participantId, Guid customerId, [FromForm] Guid[]? viewerIds, CancellationToken cancellationToken)
    {
        var requestedViewerIds = (viewerIds ?? []).Distinct().ToArray();
        var updated = await participantApiClient.UpdateParticipantViewersAsync(participantId, requestedViewerIds, cancellationToken);
        if (!updated)
        {
            LogParticipantNotFoundMessage(logger, participantId, null);
            return NotFound();
        }

        TempData.SetNotification(NotificationType.Success, "Participant viewers updated successfully.");

        // Legacy ParticipantViewers.aspx.vb BtnSave_Click calls LeavePage(), returning to the
        // participant list - not back to this screen.
        return RedirectToAction(nameof(Index), new { customerId });
    }

    private async Task PopulatePendingCountryOptionsAsync(PendingParticipantUpdateFormViewModel model, CancellationToken cancellationToken)
    {
        var countries = await lookupApiClient.GetCountriesAsync(cancellationToken);
        model.CountryOptions = countries
            .Select(c => new SelectListItem(c.Country, c.CountryId.ToString()))
            .Prepend(new SelectListItem("- Please Select -", Guid.Empty.ToString()))
            .ToList();
    }

    // Field order and labels reproduce legacy PendingParticipantUpdateDetails.aspx exactly.
    private static PendingParticipantUpdateDetailsViewModel BuildPendingParticipantUpdateDetailsViewModel(
        PendingParticipantUpdateComparisonResponse comparison,
        Dictionary<Guid, string> countryNames)
    {
        var current = comparison.Current;
        var pending = comparison.Pending;

        List<PendingParticipantUpdateComparisonRow> participantDetails =
        [
            new("Contact Name", current.ContactName, pending.ContactName),
            new("Organisation Name", current.Organisation, pending.Organisation),
            new("Address 1", current.Address1, pending.Address1),
            new("Address 2", current.Address2, pending.Address2),
            new("Address 3", current.Address3, pending.Address3),
            new("Address 4", current.Address4, pending.Address4),
            new("Address 5", current.Address5, pending.Address5),
            new("Country", countryNames.GetValueOrDefault(current.CountryId, string.Empty), countryNames.GetValueOrDefault(pending.CountryId, string.Empty)),
            new("Telephone", current.Telephone, pending.Telephone),
            new("Fax", current.Fax, pending.Fax),
            new("Email (Primary)", current.Email, pending.Email),
            new("Email (Secondary)", current.Email2, pending.Email2)
        ];

        return new PendingParticipantUpdateDetailsViewModel(current.ParticipantId, current.LabCode, current.LabName, participantDetails);
    }

    private static PendingParticipantUpdateFormViewModel ToPendingFormViewModel(PendingParticipantUpdateComparisonResponse comparison)
    {
        var pending = comparison.Pending;
        return new PendingParticipantUpdateFormViewModel
        {
            ParticipantId = comparison.Current.ParticipantId,
            LabCode = comparison.Current.LabCode,
            LabName = comparison.Current.LabName,
            ContactName = pending.ContactName,
            Organisation = pending.Organisation,
            Address1 = pending.Address1,
            Address2 = pending.Address2,
            Address3 = pending.Address3,
            Address4 = pending.Address4,
            Address5 = pending.Address5,
            CountryId = pending.CountryId,
            Telephone = pending.Telephone,
            Fax = pending.Fax,
            Email = pending.Email,
            Email2 = pending.Email2
        };
    }

    private static PendingParticipantUpdateSaveRequest ToPendingSaveRequest(PendingParticipantUpdateFormViewModel model) => new(
        model.ContactName ?? string.Empty,
        model.Organisation ?? string.Empty,
        model.Address1 ?? string.Empty,
        model.Address2 ?? string.Empty,
        model.Address3 ?? string.Empty,
        model.Address4 ?? string.Empty,
        model.Address5 ?? string.Empty,
        model.CountryId.GetValueOrDefault(),
        model.Telephone ?? string.Empty,
        model.Fax ?? string.Empty,
        model.Email ?? string.Empty,
        model.Email2 ?? string.Empty);

    private void AddErrors(IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        foreach (var kvp in fieldErrors)
        {
            foreach (var error in kvp.Value)
            {
                ModelState.AddModelError(kvp.Key, error);
            }
        }
    }

    private static ParticipantFormViewModel ToFormViewModel(ParticipantResponse participant) => new()
    {
        ParticipantId = participant.ParticipantId,
        CustomerId = participant.CustomerId,
        SsoId = participant.SsoId,
        LabCode = participant.LabCode,
        LabName = participant.LabName,
        LabTypeId = participant.LabTypeId,
        ContactName = participant.ContactName,
        Organisation = participant.Organisation,
        Address1 = participant.Address1,
        Address2 = participant.Address2,
        Address3 = participant.Address3,
        Address4 = participant.Address4,
        Address5 = participant.Address5,
        CountryId = participant.CountryId,
        Telephone = participant.Telephone,
        Fax = participant.Fax,
        Email = participant.Email,
        Email2 = participant.Email2,
        Comments = participant.Comments,
        IsActive = participant.IsActive,
        InactiveDate = participant.InactiveDate
    };

    // SsoId is a disabled field (server-generated/preserved, never user-editable - see
    // ParticipantService.UpdateParticipantAsync/CreateParticipantAsync) and InactiveDate has no
    // form input at all, so a posted-back model on a failed Edit submission has them blank/default -
    // re-fetch the persisted participant to restore them for redisplay.
    private async Task RestoreDisplayOnlyFieldsAsync(ParticipantFormViewModel model, Guid participantId, CancellationToken cancellationToken)
    {
        var participant = await participantApiClient.GetParticipantAsync(participantId, cancellationToken);
        if (participant is null)
        {
            return;
        }

        model.SsoId = participant.SsoId;
        model.InactiveDate = participant.InactiveDate;
    }

    // Fetches the LabType/Country reference lists once per request and shapes them into
    // SelectListItem so _ParticipantForm.cshtml can render <select asp-items="..."> - matches the
    // legacy DropDownLabType/DropDownCountry DataBind() calls in Participant.aspx.vb LoadLabelNames().
    private async Task PopulateLookupOptionsAsync(ParticipantFormViewModel model, CancellationToken cancellationToken)
    {
        var countriesTask = lookupApiClient.GetCountriesAsync(cancellationToken);
        var labTypesTask = lookupApiClient.GetLabTypesAsync(cancellationToken);
        await Task.WhenAll(countriesTask, labTypesTask);

        // Country is validated as required by ValidatorCountryRequired in the legacy form, so a
        // blank option is offered - matches DropDownCountry.Items.Insert(0, New ListItem(...)).
        model.CountryOptions = countriesTask.Result
            .Select(c => new SelectListItem(c.Country, c.CountryId.ToString()))
            .Prepend(new SelectListItem("- Please Select -", Guid.Empty.ToString()))
            .ToList();

        // LabType has no blank option in the legacy DropDownLabType, so none is added here either.
        model.LabTypeOptions = labTypesTask.Result
            .Select(l => new SelectListItem(l.Name, l.LabTypeId.ToString()))
            .ToList();
    }

    // Fetches the parent Customer's contact details so the "Copy from customer contact" button
    // (mirrors legacy ButtonCopyDetails_Click) can copy them client-side with no extra round trip.
    // Missing/unreachable Customer is not fatal here - the button just has nothing to copy.
    private async Task PopulateCustomerContactAsync(ParticipantFormViewModel model, Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await customerApiClient.GetCustomerAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return;
        }

        model.CustomerName = customer.Name;
        model.CustomerQalNumber = customer.QalNumber;
        model.CustomerContactName = customer.ContactName;
        model.CustomerOrganisation = customer.Organisation;
        model.CustomerAddress1 = customer.Address1;
        model.CustomerAddress2 = customer.Address2;
        model.CustomerAddress3 = customer.Address3;
        model.CustomerAddress4 = customer.Address4;
        model.CustomerAddress5 = customer.Address5;
        model.CustomerCountryId = customer.CountryId;
        model.CustomerTelephone = customer.Telephone;
        model.CustomerFax = customer.Fax;
        model.CustomerEmail = customer.Email;
    }

    private async Task<string> GenerateLabCodeAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var participants = await participantApiClient.GetParticipantsAsync(customerId, includeInactive: true, cancellationToken);
        var usedCodes = participants
            .Select(p => int.TryParse(p.LabCode, out var parsed) ? parsed : int.MinValue)
            .Where(code => code >= 1000)
            .ToHashSet();

        var availableCodes = new List<int>();
        for (var candidate = 1000; candidate < 2000 && availableCodes.Count < 1000; candidate++)
        {
            if (!usedCodes.Contains(candidate))
            {
                availableCodes.Add(candidate);
            }
        }

        if (availableCodes.Count == 0)
        {
            var fallback = 1000 + Random.Shared.Next(9000);
            return fallback.ToString(CultureInfo.InvariantCulture);
        }

        var nextCode = availableCodes[Random.Shared.Next(availableCodes.Count)];
        return nextCode.ToString(CultureInfo.InvariantCulture);
    }
}
