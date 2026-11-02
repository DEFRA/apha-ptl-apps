using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Polly;
using PTL.ApiClient;
using PTL.Common.Auth;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Features.Home;

// Privacy is external-facing only; the internal application has no such page.
public class HomeController(IApiClient apiClient, ISystemMessageApiClient systemMessageApiClient, ICreateOrderApiClient createOrderApiClient) : HomeControllerBase(apiClient)
{
    // Overrides the shared HomeControllerBase.Index() (used as-is by PTL.InternalWeb) to show the
    // CIDM display name and resolved roles, plus the admin-authored "Important Message" banner
    // (tblExtWebsiteMessage - see legacy ExternalWeb Home.aspx) - external-sign-in-specific, so it
    // doesn't belong in the shared base used by both front-ends.
    public override async Task<IActionResult> Index()
    {
        var displayName = User.GetDisplayName(preferredClaimType: ExternalUserClaimTypes.DisplayName) ?? string.Empty;
        var importantMessage = await systemMessageApiClient.GetImportantMessageAsync(HttpContext.RequestAborted);
        var (canCreateCurrent, canCreateNext) = await GetOrderEligibilityAsync(HttpContext.RequestAborted);
        return View(new HomeIndexViewModel(displayName, User.GetResolvedExternalRoles(), importantMessage.ImportantMessage, canCreateCurrent, canCreateNext));
    }

    // Legacy Home.aspx hides each "Create order" card once an order for that year is submitted. The order pages enforce
    // the same rules themselves, so a failed lookup shows both cards rather than breaking Home.
    private async Task<(bool Current, bool Next)> GetOrderEligibilityAsync(CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null || !User.CanOrderOnline())
        {
            return (true, true);
        }

        try
        {
            var eligibility = await createOrderApiClient.GetEligibilityAsync(participantId.Value, cancellationToken);
            return eligibility is null ? (true, true) : (eligibility.CanCreateCurrentYearOrder, eligibility.CanCreateNextYearOrder);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ExecutionRejectedException)
        {
            return (true, true);
        }
    }

    // Legal/informational page - deliberately anonymous, like PTL.ExternalWeb's other
    // [AllowAnonymous] pages (ApiStatus, Error), since it must be reachable before sign-in.
    [AllowAnonymous]
    public IActionResult Privacy() => View();

    // Legacy ViewInformation.aspx, linked from Index's static "Important" banner - shows
    // tblExtWebsiteMessage.fldMessage, requires sign-in (same as legacy).
    public async Task<IActionResult> Information()
    {
        var message = await systemMessageApiClient.GetMessageAsync(HttpContext.RequestAborted);
        return View(new InformationViewModel(message.Message));
    }
}
