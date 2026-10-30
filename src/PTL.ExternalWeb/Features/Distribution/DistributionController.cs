using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;
using PTL.SharedUI.Pagination;

namespace PTL.ExternalWeb.Features.Distribution;

// Legacy DistributionsCurrent.aspx/DistributionsPrevious.aspx (tracker rows 8, 10) - both actions
// share one controller since they're the same "Distributions & Results" module. Previous/Results
// implement story 744650 (View Results); Current (Enter Results) stays a placeholder.
[Authorize(Policy = ExternalPolicies.Participant)]
public sealed class DistributionController(IDistributionApiClient distributionApiClient) : Controller
{
    private const int DefaultPageSize = 15;

    private static readonly string[] ValidSortColumns = ["reference", "schemeCode", "schemeName", "distributionDate"];
    private static readonly string[] ValidSortDirections = ["asc", "desc"];

    public IActionResult Current() => View("_ComingSoonBody", "Enter results");

    // Legacy DistributionsPrevious.aspx - Year/Month dropdowns are exact matches, the 3 text
    // criteria are "contains" (Scheme Name matches when every typed word is contained), mirroring
    // DistributionsPast.ExecuteSearch. Default sort is DistributionDate descending.
    public async Task<IActionResult> Previous(
        int? year,
        int? month,
        string? distributionReference,
        string? schemeCode,
        string? schemeName,
        int page = 1,
        int pageSize = DefaultPageSize,
        string sortBy = "distributionDate",
        string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        sortBy = ValidSortColumns.Contains(sortBy) ? sortBy : "distributionDate";
        sortDir = ValidSortDirections.Contains(sortDir) ? sortDir : "desc";
        page = Math.Max(page, 1);
        pageSize = PaginationModel.AvailablePageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

        var participantId = User.GetParticipantId();
        if (participantId is null)
        {
            return View(new DistributionPreviousListViewModel([], 0, page, pageSize, sortBy, sortDir, year, month, distributionReference, schemeCode, schemeName));
        }

        var history = await distributionApiClient.GetParticipantDistributionHistoryAsync(participantId.Value, cancellationToken);

        var filtered = Filter(history.Distributions, year, month, distributionReference, schemeCode, schemeName);
        var sorted = Sort(filtered, sortBy, sortDir);
        var paged = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return View(new DistributionPreviousListViewModel(
            paged, sorted.Count, page, pageSize, sortBy, sortDir, year, month, distributionReference, schemeCode, schemeName));
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

        IOrderedEnumerable<ParticipantPastDistributionResponse> ordered = sortBy switch
        {
            "reference" => Order(d => d.DistributionReference),
            "schemeCode" => Order(d => d.SchemeCode),
            "schemeName" => Order(d => d.SchemeName),
            _ => Order(d => d.DistributionDate),
        };

        return [.. ordered];
    }
}
