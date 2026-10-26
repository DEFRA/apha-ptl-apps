using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Features.Scheme;

// Legacy ExternalWeb SchemeList.aspx - read-only commercial/non-commercial scheme list for the
// signed-in participant (see docs/Parity-Analysis/01-Module-Migration-Inventory.md "Schemes").
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class SchemeController(ISchemeApiClient schemeApiClient) : Controller
{
    public async Task<IActionResult> Index()
    {
        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return View(new SchemeListViewModel([], []));
        }

        var result = await schemeApiClient.GetSchemesForParticipantAsync(participantId.Value, HttpContext.RequestAborted);
        return View(new SchemeListViewModel(result.CommercialSchemes, result.NonCommercialSchemes));
    }
}
