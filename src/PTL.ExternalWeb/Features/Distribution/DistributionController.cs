using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.ViewSupport;
using PTL.SharedUI.Pagination;

namespace PTL.ExternalWeb.Features.Distribution;

// Legacy DistributionsCurrent.aspx/DistributionsPrevious.aspx (tracker rows 8, 10) - both actions
// share one controller since they're the same "Distributions & Results" module. Previous/Results
// implement story 744650 (View Results); Current (Enter Results) stays a placeholder.
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class DistributionController(IDistributionApiClient distributionApiClient) : Controller
{
    private static readonly string[] ValidSortColumns = ["reference", "schemeCode", "schemeName", "distributionDate"];
    private static readonly string[] ValidSortDirections = ["asc", "desc"];

    public IActionResult Current() => View("_ComingSoonBody", "Enter results");

    // Legacy DistributionsPrevious.aspx - Year/Month dropdowns are exact matches, the 3 text
    // criteria are "contains" (Scheme Name matches when every typed word is contained), mirroring
    // DistributionsPast.ExecuteSearch. Default sort is DistributionDate descending.
    public async Task<IActionResult> Previous([FromQuery] ReferenceSchemeListQuery query, CancellationToken cancellationToken = default)
    {
        var sortBy = ValidSortColumns.Contains(query.SortBy) ? query.SortBy : "distributionDate";
        var sortDir = ValidSortDirections.Contains(query.SortDir) ? query.SortDir : "desc";
        var page = Math.Max(query.Page, 1);
        var pageSize = PaginationModel.AvailablePageSizes.Contains(query.PageSize) ? query.PageSize : ReferenceSchemeListQuery.DefaultPageSize;

        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return View(new DistributionPreviousListViewModel([], 0, page, pageSize, sortBy, sortDir, query.Year, query.Month, query.DistributionReference, query.SchemeCode, query.SchemeName));
        }

        var history = await distributionApiClient.GetParticipantDistributionHistoryAsync(participantId.Value, cancellationToken);

        var filtered = Filter(history.Distributions, query.Year, query.Month, query.DistributionReference, query.SchemeCode, query.SchemeName);
        var sorted = Sort(filtered, sortBy, sortDir);
        var paged = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return View(new DistributionPreviousListViewModel(
            paged, sorted.Count, page, pageSize, sortBy, sortDir, query.Year, query.Month, query.DistributionReference, query.SchemeCode, query.SchemeName));
    }

    // Legacy ResultsView.aspx - read-only, no Save/Submit/Edit/Delete. Reuses the same
    // actual-results data as InternalWeb's Actual Results screen (see
    // src/PTL.Core/Distribution/ActualResultsService.cs), resolved against the signed-in
    // participant server-side - NotFound when this participant has no results for this
    // distribution, which also covers the Access Restrictions AC (a tampered
    // monthlyDistributionSchemeId that belongs to someone else resolves to nothing).
    public async Task<IActionResult> Results(Guid monthlyDistributionSchemeId, CancellationToken cancellationToken)
    {
        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return NotFound();
        }

        var page = await distributionApiClient.GetActualResultsForParticipantAsync(monthlyDistributionSchemeId, participantId.Value, cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    private static List<ParticipantPastDistributionResponse> Filter(
        IReadOnlyList<ParticipantPastDistributionResponse> distributions,
        int? year, int? month, string? distributionReference, string? schemeCode, string? schemeName) =>
        [.. distributions.Where(d =>
            (year is null or 0 || d.YearId == year)
            && (month is null or 0 || d.MonthId == month)
            && Contains(d.DistributionReference, distributionReference)
            && Contains(d.SchemeCode, schemeCode)
            && ContainsAllWords(d.SchemeName, schemeName))];

    private static bool Contains(string value, string? term) =>
        string.IsNullOrWhiteSpace(term) || value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAllWords(string value, string? term) =>
        string.IsNullOrWhiteSpace(term)
        || term.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => value.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static List<ParticipantPastDistributionResponse> Sort(
        IReadOnlyList<ParticipantPastDistributionResponse> distributions, string sortBy, string sortDir)
    {
        IOrderedEnumerable<ParticipantPastDistributionResponse> Order<TKey>(Func<ParticipantPastDistributionResponse, TKey> keySelector) =>
            sortDir == "desc" ? distributions.OrderByDescending(keySelector) : distributions.OrderBy(keySelector);

        var ordered = sortBy switch
        {
            "reference" => Order(d => d.DistributionReference),
            "schemeCode" => Order(d => d.SchemeCode),
            "schemeName" => Order(d => d.SchemeName),
            _ => Order(d => d.DistributionDate),
        };

        return [.. ordered];
    }
}
