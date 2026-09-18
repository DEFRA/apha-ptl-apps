using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Participant;

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

    public async Task<IActionResult> Index(Guid customerId, string? searchTerm = null, bool includeInactive = false, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
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

        return View(participant);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid customerId, CancellationToken cancellationToken)
    {
        var model = new ParticipantFormViewModel { CustomerId = customerId };
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
            await PopulateLookupOptionsAsync(model, cancellationToken);
            await PopulateCustomerContactAsync(model, customerId, cancellationToken);
            return View(model);
        }

        try
        {
            var created = await participantApiClient.CreateParticipantAsync(new CreateParticipantRequest(
                customerId,
                model.SsoId,
                model.LabCode,
                model.LabName,
                model.LabTypeId,
                model.ContactName,
                model.Organisation,
                model.Address1,
                model.Address2,
                model.Address3,
                model.Address4,
                model.Address5,
                model.CountryId,
                model.Telephone,
                model.Fax,
                model.Email,
                model.Email2,
                model.Comments,
                model.IsActive), cancellationToken);

            return RedirectToAction(nameof(Details), new { id = created.ParticipantId });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create participant for customer {CustomerId}", customerId);
            ModelState.AddModelError(string.Empty, "Unable to create this participant. Please review the details and try again.");
            model.CustomerId = customerId;
            await PopulateLookupOptionsAsync(model, cancellationToken);
            await PopulateCustomerContactAsync(model, customerId, cancellationToken);
            return View(model);
        }
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
            await PopulateCustomerContactAsync(model, model.CustomerId, cancellationToken);
            return View(model);
        }

        try
        {
            var updated = await participantApiClient.UpdateParticipantAsync(id, new UpdateParticipantRequest(
                model.CustomerId,
                model.SsoId,
                model.LabCode,
                model.LabName,
                model.LabTypeId,
                model.ContactName,
                model.Organisation,
                model.Address1,
                model.Address2,
                model.Address3,
                model.Address4,
                model.Address5,
                model.CountryId,
                model.Telephone,
                model.Fax,
                model.Email,
                model.Email2,
                model.Comments,
                model.IsActive), cancellationToken);

            return updated is null ? NotFound() : RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update participant {ParticipantId}", id);
            ModelState.AddModelError(string.Empty, "Unable to update this participant. Please review the changes and try again.");
            await RestoreDisplayOnlyFieldsAsync(model, id, cancellationToken);
            await PopulateLookupOptionsAsync(model, cancellationToken);
            await PopulateCustomerContactAsync(model, model.CustomerId, cancellationToken);
            return View(model);
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
}
