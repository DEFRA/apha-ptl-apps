using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Authorization;

namespace PTL.ExternalWeb.Features.Tabulation;

// Legacy Tabulations.aspx/TabulationView.aspx (tracker rows 15-16) - placeholder only.
[Authorize(Policy = ExternalPolicies.Viewer)]
public sealed class TabulationController : Controller
{
    public IActionResult Index() => View("_ComingSoonBody", "View reports");
}
