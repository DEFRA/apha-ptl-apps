using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;
using PTL.SharedUI.Pagination;

namespace PTL.ExternalWeb.Features.Scheme;

// Legacy ExternalWeb SchemeList.aspx - read-only commercial/non-commercial scheme list for the
// signed-in participant (see docs/Parity-Analysis/01-Module-Migration-Inventory.md "Schemes").
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class SchemeController(ISchemeApiClient schemeApiClient) : Controller
{
    // Legacy GridView page size for this screen - distinct from PaginationModel.DefaultPageSize
    // (25), which is the InternalWeb admin-screen convention.
    private const int DefaultPageSize = 15;

    // Pagination is applied in-memory over the already-fetched full list, not as a paged
    // repository/API query - GetSchemesForParticipantAsync reuses the existing legacy stored
    // procedures as-is (see SchemeService), which already return the full set.
    public async Task<IActionResult> Index(int page = 1, int pageSize = DefaultPageSize)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return View(new SchemeListViewModel([], 0, 1, DefaultPageSize, []));
        }

        var result = await schemeApiClient.GetSchemesForParticipantAsync(participantId.Value, HttpContext.RequestAborted);

        page = Math.Max(page, 1);
        pageSize = PaginationModel.AvailablePageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

        var pagedCommercialSchemes = result.CommercialSchemes
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return View(new SchemeListViewModel(pagedCommercialSchemes, result.CommercialSchemes.Count, page, pageSize, result.NonCommercialSchemes));
    }
}
