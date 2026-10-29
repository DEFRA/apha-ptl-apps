using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Authorization;

namespace PTL.ExternalWeb.Features.Order;

// Legacy PendingContractOrder.aspx (tracker row 12, "Deferred") - placeholder only; the wizard
// itself is deliberately out of scope until simpler Account & Orders screens prove the pattern.
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class OrderController : Controller
{
    public IActionResult Index() => View("_ComingSoonBody", "Create order");
}
