using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Features.Home;

// Privacy is external-facing only; the internal application has no such page.
public class HomeController(IApiClient apiClient) : HomeControllerBase(apiClient)
{
    // Overrides the shared HomeControllerBase.Index() (used as-is by PTL.InternalWeb) to show the
    // CIDM display name and resolved roles - external-sign-in-specific, so it doesn't belong in
    // the shared base used by both front-ends.
    public override IActionResult Index()
    {
        var displayName = User.FindFirst(ExternalUserClaimTypes.DisplayName)?.Value ?? User.Identity?.Name ?? string.Empty;
        var roles = User.FindFirst(ExternalUserClaimTypes.ResolvedRoles)?.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];

        return View(new HomeIndexViewModel(displayName, roles));
    }

    // Legal/informational page - deliberately anonymous, like PTL.ExternalWeb's other
    // [AllowAnonymous] pages (ApiStatus, Error), since it must be reachable before sign-in.
    [AllowAnonymous]
    public IActionResult Privacy() => View();
}
