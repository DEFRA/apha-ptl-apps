using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;
using PTL.SharedUI.Pagination;

namespace PTL.ExternalWeb.Features.Tabulation;

// Legacy Tabulations.aspx/TabulationView.aspx/AssessmentResults.aspx (tracker rows 15-16) - "View
// Reports". Visible to Participant, Viewer and Test Consultant; every lookup is scoped to the
// caller's own access by PTL.Api's "for-user" tabulation endpoints (ParticipantTabulationService),
// never by trusting a bare id from the URL - see that service's own comments for why.
[Authorize(Policy = ExternalPolicies.ViewReports)]
public sealed class TabulationController(IDistributionApiClient distributionApiClient) : Controller
{
    private const int DefaultPageSize = 15;

    private static readonly string[] ValidSortColumns = ["reference", "schemeCode", "schemeName", "tabulationName", "distributionDate"];
    private static readonly string[] ValidSortDirections = ["asc", "desc"];

    // Legacy Tabulations.aspx - Year/Month dropdowns are exact matches, the 3 text criteria are
    // "contains" (Scheme Name matches when every typed word is contained), mirroring
    // DistributionController.Previous's DistributionsPast.ExecuteSearch pattern. Default sort is
    // DistributionDate descending.
    public async Task<IActionResult> Index(
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

        var (participantId, viewerId, testConsultantId) = CallerIds();

        var list = await distributionApiClient.GetTabulationsForUserAsync(participantId, viewerId, testConsultantId, cancellationToken);

        var filtered = Filter(list.Tabulations, year, month, distributionReference, schemeCode, schemeName);
        var sorted = Sort(filtered, sortBy, sortDir);
        var paged = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return View(new TabulationListViewModel(
            paged, sorted.Count, page, pageSize, sortBy, sortDir, year, month, distributionReference, schemeCode, schemeName));
    }

    // Legacy TabulationView.aspx - read only, no participant/viewer switcher (unlike InternalWeb's
    // staff-facing Tabulation/PublishedTabulation, this is always the signed-in user's own report).
    public async Task<IActionResult> Report(Guid publishedTabulationId, CancellationToken cancellationToken)
    {
        var (participantId, viewerId, testConsultantId) = CallerIds();
        var report = await distributionApiClient.GetPublishedTabulationForUserAsync(
            publishedTabulationId, participantId, viewerId, testConsultantId, cancellationToken);
        return report is null ? NotFound() : View(new TabulationReportPageViewModel(report));
    }

    // Legacy AssessmentResults.aspx - rendered as a data table (see AssessmentResultsPageViewModel).
    public async Task<IActionResult> AssessmentResults(Guid publishedTabulationId, Guid testId, CancellationToken cancellationToken)
    {
        var (participantId, viewerId, testConsultantId) = CallerIds();
        var results = await distributionApiClient.GetAssessmentResultsForUserAsync(
            publishedTabulationId, testId, participantId, viewerId, testConsultantId, cancellationToken);
        return results is null ? NotFound() : View(new AssessmentResultsPageViewModel(publishedTabulationId, testId, results));
    }

    // Legacy AttachmentDisplay "View Supporting Document" (Attachment.aspx), without the session hand-off.
    public async Task<IActionResult> Attachment(Guid publishedTabulationId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var (participantId, viewerId, testConsultantId) = CallerIds();
        var file = await distributionApiClient.GetPublishedTabulationAttachmentForUserAsync(
            publishedTabulationId, attachmentId, participantId, viewerId, testConsultantId, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        Response.Headers.ContentDisposition = file.ContentDisposition;
        return File(file.Content, file.ContentType);
    }

    // PDF export is a separate Reporting workstream deliverable - placeholder only until then.
    public IActionResult GeneratePdf(Guid publishedTabulationId) => View("_ComingSoonBody", "Generate PDF");

    private (Guid? ParticipantId, Guid? ViewerId, Guid? TestConsultantId) CallerIds() =>
        (User.GetParticipantId(), User.GetViewerId(), User.GetTestConsultantId());

    private static List<PublishedTabulationSummaryResponse> Filter(
        IReadOnlyList<PublishedTabulationSummaryResponse> tabulations,
        int? year, int? month, string? distributionReference, string? schemeCode, string? schemeName) =>
        [.. tabulations.Where(t =>
            (year is null or 0 || t.YearId == year)
            && (month is null or 0 || t.MonthId == month)
            && Contains(t.DistributionReference, distributionReference)
            && Contains(t.SchemeCode, schemeCode)
            && ContainsAllWords(t.SchemeName, schemeName))];

    private static bool Contains(string value, string? term) =>
        string.IsNullOrWhiteSpace(term) || value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAllWords(string value, string? term) =>
        string.IsNullOrWhiteSpace(term)
        || term.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => value.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static List<PublishedTabulationSummaryResponse> Sort(
        IReadOnlyList<PublishedTabulationSummaryResponse> tabulations, string sortBy, string sortDir)
    {
        IOrderedEnumerable<PublishedTabulationSummaryResponse> Order<TKey>(Func<PublishedTabulationSummaryResponse, TKey> keySelector) =>
            sortDir == "desc" ? tabulations.OrderByDescending(keySelector) : tabulations.OrderBy(keySelector);

        IOrderedEnumerable<PublishedTabulationSummaryResponse> ordered = sortBy switch
        {
            "reference" => Order(t => t.DistributionReference),
            "schemeCode" => Order(t => t.SchemeCode),
            "schemeName" => Order(t => t.SchemeName),
            "tabulationName" => Order(t => t.TabulationName),
            _ => Order(t => t.DistributionDate),
        };

        return [.. ordered];
    }
}
