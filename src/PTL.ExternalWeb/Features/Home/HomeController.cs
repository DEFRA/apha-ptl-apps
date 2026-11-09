using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Polly;
using PTL.ApiClient;
using PTL.Common.Auth;
using PTL.Contracts.CreateOrder;
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
        var eligibility = await GetOrderEligibilityAsync(HttpContext.RequestAborted);
        return View(new HomeIndexViewModel(
            displayName,
            User.GetResolvedExternalRoles(),
            importantMessage.ImportantMessage,
            eligibility.CanCreateCurrentYearOrder,
            eligibility.CanCreateNextYearOrder,
            eligibility.CurrentYear,
            eligibility.NextYear));
    }

    // Legacy Home.aspx keeps each "Create order" card but disables it once an order for that year is submitted. The order
    // pages enforce the same rules themselves, so a failed lookup shows both cards enabled rather than breaking Home.
    private async Task<CreateOrderEligibilityResponse> GetOrderEligibilityAsync(CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null || !User.CanOrderOnline())
        {
            return AllowBoth;
        }

        try
        {
            return await createOrderApiClient.GetEligibilityAsync(participantId.Value, cancellationToken) ?? AllowBoth;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ExecutionRejectedException)
        {
            return AllowBoth;
        }
    }

    private static readonly CreateOrderEligibilityResponse AllowBoth = new(true, true, true);

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
