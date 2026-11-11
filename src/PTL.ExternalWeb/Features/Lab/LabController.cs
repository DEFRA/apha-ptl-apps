using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Contracts.ExternalUser;
using PTL.ExternalWeb.Features.Account;
using PTL.SharedUI.Notifications;

namespace PTL.ExternalWeb.Features.Lab;

// Choosing the lab to work on (and switching to another) for a Participant or Lab User with more than one active lab.
// Which labs are offered, and whether a chosen one is allowed, are always decided by PTL.Api from the user's CIDM contact
// id - never from anything on the form - so a lab id that is not one of the user's own labs is simply refused.
[SkipLabSelection]
public sealed class LabController(IExternalUserApiClient externalUserApiClient) : Controller
{
    private const string SelectLabMessage = "Select a lab";

    [HttpGet]
    public async Task<IActionResult> Select(CancellationToken cancellationToken)
    {
        if (User.GetContactId() is not { } contactId)
        {
            return NoLabAccess();
        }

        var labs = await externalUserApiClient.GetLabsAsync(contactId, cancellationToken);
        switch (labs.Count)
        {
            case 0:
                return NoLabAccess();

            // Nothing to choose from (e.g. every other lab was made inactive since sign-in), so carry on with the one lab.
            case 1:
                var only = await externalUserApiClient.GetLabAsync(contactId, labs[0].ParticipantId, cancellationToken);
                if (only is null)
                {
                    return NoLabAccess();
                }

                await SignInForLabAsync(only, labs.Count);
                return RedirectToAction("Index", "Home");

            default:
                var current = User.GetParticipantId();
                return View(new LabSelectionViewModel
                {
                    Labs = labs,
                    IsSwitch = current is not null,
                    SelectedLabId = labs.Any(lab => lab.ParticipantId == current) ? current : null
                });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Select(LabSelectionViewModel model, CancellationToken cancellationToken)
    {
        if (User.GetContactId() is not { } contactId)
        {
            return NoLabAccess();
        }

        var labs = await externalUserApiClient.GetLabsAsync(contactId, cancellationToken);
        if (labs.Count == 0)
        {
            return NoLabAccess();
        }

        var current = User.GetParticipantId();
        model.Labs = labs;
        model.IsSwitch = current is not null;

        var lab = model.SelectedLabId is { } selected
            ? await externalUserApiClient.GetLabAsync(contactId, selected, cancellationToken)
            : null;
        if (lab is null)
        {
            // One message for "nothing chosen" and "not one of your labs", so a lab id cannot be probed through the form.
            ModelState.AddModelError(nameof(LabSelectionViewModel.SelectedLabId), SelectLabMessage);
            return View(model);
        }

        await SignInForLabAsync(lab, labs.Count);

        if (current is not null && current != lab.ParticipantId)
        {
            TempData.SetNotification(NotificationType.Success, $"You are now working on lab {lab.LabCode} – {lab.LabName}.");
        }

        return RedirectToAction("Index", "Home");
    }

    // Re-issues the session cookie with the chosen lab's claims, keeping its tokens and lifetime, so the lab is
    // fixed for the session and every page reads it from the claims as before.
    private async Task SignInForLabAsync(ExternalLabContext lab, int labCount)
    {
        var authentication = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (authentication.Principal is null)
        {
            return;
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            LabSession.WithLab(authentication.Principal, lab, labCount),
            authentication.Properties);
    }

    // The user no longer has an active lab (access ended since sign-in), so there is nothing to work on - the same page
    // as signing in with no active lab, which offers sign out.
    private RedirectResult NoLabAccess() => Redirect(NotPermittedReasons.PathFor(NotPermittedReasons.NoActiveLab));
}
