using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Contracts.LabUsers;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;
using PTL.SharedUI.Notifications;

namespace PTL.ExternalWeb.Features.LabUser;

// A lab's primary contact managing the people who can sign in to the lab (deck flows 4.1 and 4.3). Only the primary contact:
// a Lab User holds the Participant role too but is refused here by the PrimaryContact policy. Everything is scoped to the lab
// the user is working on (the signed-in session's participant id), never to an id from the request, so one lab's users can
// never be reached from another.
[Authorize(Policy = ExternalPolicies.PrimaryContact)]
public sealed class LabUserController(ILabUserApiClient labUserApiClient) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (User.GetParticipantId() is not { } participantId)
        {
            return NotFound();
        }

        var list = await labUserApiClient.GetListAsync(participantId, cancellationToken);
        return list is null ? NotFound() : View(new LabUserListViewModel(list.LabCode, list.LabName, list.Users));
    }

    [HttpGet]
    public async Task<IActionResult> Add(CancellationToken cancellationToken)
    {
        if (User.GetParticipantId() is not { } participantId)
        {
            return NotFound();
        }

        var list = await labUserApiClient.GetListAsync(participantId, cancellationToken);
        return list is null
            ? NotFound()
            : View(new LabUserFormViewModel { LabCode = list.LabCode, LabName = list.LabName });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(LabUserInput input, CancellationToken cancellationToken)
    {
        if (User.GetParticipantId() is not { } participantId)
        {
            return NotFound();
        }

        var result = await labUserApiClient.AddAsync(participantId, ToRequest(input), cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        if (!result.Success)
        {
            AddErrors(result.FieldErrors);
            var model = new LabUserFormViewModel { Name = input.Name, Email = input.Email, IsActive = input.IsActive };
            await FillLabAsync(model, participantId, cancellationToken);
            return View(model);
        }

        TempData.SetNotification(NotificationType.Success, result.Outcome == LabUserSaveOutcome.AddedExistingAccount
            ? "Lab user added. This person already had an account, so they can now sign in to this lab as well."
            : "Lab user added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid labUserId, CancellationToken cancellationToken)
    {
        if (User.GetParticipantId() is not { } participantId)
        {
            return NotFound();
        }

        var details = await labUserApiClient.GetAsync(participantId, labUserId, cancellationToken);
        return details is null ? NotFound() : View(ToForm(details));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid labUserId, LabUserInput input, CancellationToken cancellationToken)
    {
        if (User.GetParticipantId() is not { } participantId)
        {
            return NotFound();
        }

        var details = await labUserApiClient.GetAsync(participantId, labUserId, cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        // A person shared with other labs shows their name and email as plain text, so the form posts neither:
        // what is saved is always what is on record, and only Active can change.
        var current = details.User;
        if (!current.CanChangeDetails)
        {
            input.Name = current.Name;
            input.Email = current.Email;
        }

        var result = await labUserApiClient.UpdateAsync(participantId, labUserId, ToRequest(input), cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        if (!result.Success)
        {
            AddErrors(result.FieldErrors);
            var form = ToForm(details);
            form.Name = input.Name;
            form.Email = input.Email;
            form.IsActive = input.IsActive;
            return View(form);
        }

        TempData.SetNotification(NotificationType.Success, UpdatedMessage(result.Outcome, input.IsActive));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendInstructions(Guid labUserId, CancellationToken cancellationToken)
    {
        if (User.GetParticipantId() is not { } participantId)
        {
            return NotFound();
        }

        var response = await labUserApiClient.SendLoginInstructionsAsync(participantId, labUserId, cancellationToken);
        if (response is null)
        {
            return NotFound();
        }

        TempData.SetNotification(
            response.Success ? NotificationType.Success : NotificationType.Error,
            response.Success ? "Login instructions sent." : response.Message ?? "The login instructions email could not be sent. Please try again.");
        return RedirectToAction(nameof(Index));
    }

    private static string UpdatedMessage(LabUserSaveOutcome outcome, bool isActive)
    {
        if (outcome == LabUserSaveOutcome.UpdatedEmailChanged)
        {
            return "Lab user updated. The email address has changed, so they need to register or sign in with the new address, and you need to send them the login instructions again.";
        }

        return isActive
            ? "Lab user updated."
            : "Lab user updated. They can no longer sign in to this lab.";
    }

    private async Task FillLabAsync(LabUserFormViewModel model, Guid participantId, CancellationToken cancellationToken)
    {
        var list = await labUserApiClient.GetListAsync(participantId, cancellationToken);
        model.LabCode = list?.LabCode ?? string.Empty;
        model.LabName = list?.LabName ?? string.Empty;
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

    private static LabUserFormViewModel ToForm(LabUserDetailsResponse details) => new()
    {
        LabUserId = details.User.LabUserId,
        Name = details.User.Name,
        Email = details.User.Email,
        IsActive = details.User.IsActive,
        LabCode = details.LabCode,
        LabName = details.LabName,
        CanChangeDetails = details.User.CanChangeDetails,
        HasSignedIn = details.User.HasSignedIn
    };

    private static LabUserSaveRequest ToRequest(LabUserInput input) =>
        new(input.Name ?? string.Empty, input.Email ?? string.Empty, input.IsActive);
}
