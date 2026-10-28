using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Contracts.Scheme;
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

    private static readonly string[] ValidSortColumns = ["code", "name", "year"];
    private static readonly string[] ValidSortDirections = ["asc", "desc"];

    // Pagination and sorting are both applied in-memory over the already-fetched full list, not as
    // a paged/sorted repository query - GetSchemesForParticipantAsync reuses the existing legacy
    // stored procedures as-is (see SchemeService), which already return the full set unsorted.
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = DefaultPageSize,
        string sortBy = "code",
        string sortDir = "asc",
        string ncSortBy = "code",
        string ncSortDir = "asc")
    {
        sortBy = ValidSortColumns.Contains(sortBy) ? sortBy : "code";
        sortDir = ValidSortDirections.Contains(sortDir) ? sortDir : "asc";
        ncSortBy = ValidSortColumns.Contains(ncSortBy) ? ncSortBy : "code";
        ncSortDir = ValidSortDirections.Contains(ncSortDir) ? ncSortDir : "asc";

        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return View(new SchemeListViewModel([], 0, 1, DefaultPageSize, sortBy, sortDir, [], ncSortBy, ncSortDir));
        }

        var result = await schemeApiClient.GetSchemesForParticipantAsync(participantId.Value, HttpContext.RequestAborted);

        page = Math.Max(page, 1);
        pageSize = PaginationModel.AvailablePageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

        var sortedCommercialSchemes = SortSchemes(result.CommercialSchemes, sortBy, sortDir);
        var pagedCommercialSchemes = sortedCommercialSchemes
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var sortedNonCommercialSchemes = SortSchemes(result.NonCommercialSchemes, ncSortBy, ncSortDir);

        return View(new SchemeListViewModel(
            pagedCommercialSchemes,
            result.CommercialSchemes.Count,
            page,
            pageSize,
            sortBy,
            sortDir,
            sortedNonCommercialSchemes,
            ncSortBy,
            ncSortDir));
    }

    private static List<ParticipantSchemeSummaryResponse> SortSchemes(
        IReadOnlyList<ParticipantSchemeSummaryResponse> schemes, string sortBy, string sortDir)
    {
        Func<ParticipantSchemeSummaryResponse, string> keySelector = sortBy switch
        {
            "name" => scheme => scheme.Name,
            "year" => scheme => scheme.Year,
            _ => scheme => scheme.Identifier,
        };

        var ordered = sortDir == "desc"
            ? schemes.OrderByDescending(keySelector, StringComparer.OrdinalIgnoreCase)
            : schemes.OrderBy(keySelector, StringComparer.OrdinalIgnoreCase);

        return ordered.ToList();
    }
}
