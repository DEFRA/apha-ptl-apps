using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ExternalWeb.Authorization;

namespace PTL.ExternalWeb.Features.Customer;

// Legacy EditCustomerDetails.aspx (tracker row 6) - placeholder only.
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class CustomerController : Controller
{
    public IActionResult Index() => View("_ComingSoonBody", "Edit customer details");
}
