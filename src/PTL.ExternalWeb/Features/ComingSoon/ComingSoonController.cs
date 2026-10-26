using Microsoft.AspNetCore.Mvc;

namespace PTL.ExternalWeb.Features.ComingSoon;

// Shared landing page every not-yet-migrated nav/card link points at (see NavigationProvider) -
// requires sign-in like the rest of the app, so it proves the navigation itself works end-to-end
// while the real screen is still pending its own migration slice.
public sealed class ComingSoonController : Controller
{
    // requiredRole is the link's own NavigationItem.RequiredRole, passed as a route value so a
    // user can't bypass nav-hiding by guessing the URL to another role's stub page.
    public IActionResult Index(string title, string? requiredRole = null)
    {
        if (requiredRole is not null && !User.IsInRole(requiredRole))
        {
            return Forbid();
        }

        return View(new ComingSoonViewModel(title));
    }
}
