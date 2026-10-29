using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Authorization;

namespace PTL.ExternalWeb.Features.Comments;

// Legacy DistributionComments.aspx/CommentsEntry.aspx (tracker rows 13-14) - placeholder only.
[Authorize(Policy = ExternalPolicies.TestConsultant)]
public sealed class CommentsController : Controller
{
    public IActionResult Index() => View("_ComingSoonBody", "Enter comments");
}
