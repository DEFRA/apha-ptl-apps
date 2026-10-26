using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Authorization;

namespace PTL.ExternalWeb.Features.Participant;

// Legacy EditParticipantDetails.aspx (tracker row 7) - placeholder only.
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class ParticipantController : Controller
{
    public IActionResult Index() => View("_ComingSoonBody", "Edit participant details");
}
