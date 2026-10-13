using Microsoft.AspNetCore.Mvc;

namespace PTL.ExternalWeb.Features.ComingSoon;

// Shared landing page every not-yet-migrated nav/card link points at (see NavigationProvider) -
// requires sign-in like the rest of the app, so it proves the navigation itself works end-to-end
// while the real screen is still pending its own migration slice.
public sealed class ComingSoonController : Controller
{
    public IActionResult Index(string title) => View(new ComingSoonViewModel(title));
}
