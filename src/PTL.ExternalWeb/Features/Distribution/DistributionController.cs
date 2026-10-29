using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Authorization;

namespace PTL.ExternalWeb.Features.Distribution;

// Legacy DistributionsCurrent.aspx/DistributionsPrevious.aspx (tracker rows 8, 10) - placeholder
// only; both actions share one controller since they're the same "Distributions & Results" module.
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class DistributionController : Controller
{
    public IActionResult Current() => View("_ComingSoonBody", "Enter results");

    public IActionResult Previous() => View("_ComingSoonBody", "View results");
}
