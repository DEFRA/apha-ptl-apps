using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PTL.ApiClient;
using PTL.Contracts.Participant;

namespace PTL.InternalWeb.Features.Participant;

public class ParticipantController(IParticipantApiClient participantApiClient, ILogger<ParticipantController> logger) : Controller
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
    public IActionResult Create(Guid customerId)
    {
        return View(new ParticipantFormViewModel { CustomerId = customerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid customerId, ParticipantFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.CustomerId = customerId;
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
            LogFailedToCreateParticipantMessage(logger, customerId, ex);
            ModelState.AddModelError(string.Empty, "Unable to create this participant. Please review the details and try again.");
            model.CustomerId = customerId;
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var participant = await participantApiClient.GetParticipantAsync(id, cancellationToken);
        return participant is null ? NotFound() : View(ToFormViewModel(participant));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ParticipantFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
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
            LogFailedToUpdateParticipantMessage(logger, id, ex);
            ModelState.AddModelError(string.Empty, "Unable to update this participant. Please review the changes and try again.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var participant = await participantApiClient.GetParticipantAsync(id, cancellationToken);
        return participant is null ? NotFound() : View(new DeactivateParticipantViewModel { ParticipantId = participant.ParticipantId, LabName = participant.LabName });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id, DeactivateParticipantViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.ParticipantId = id;
            return View(model);
        }

        try
        {
            var updated = await participantApiClient.DeactivateParticipantAsync(id, cancellationToken);
            return updated is null ? NotFound() : RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            LogFailedToDeactivateParticipantMessage(logger, id, ex);
            ModelState.AddModelError(string.Empty, "Unable to deactivate this participant.");
            model.ParticipantId = id;
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await participantApiClient.ReactivateParticipantAsync(id, cancellationToken);
            return updated is null ? NotFound() : RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            LogFailedToReactivateParticipantMessage(logger, id, ex);
            return RedirectToAction(nameof(Details), new { id });
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
        IsActive = participant.IsActive
    };
}
