using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using PTL.ApiClient.Distribution;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Authorization;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.ViewSupport;
using PTL.SharedUI.Pagination;

namespace PTL.ExternalWeb.Features.Tabulation;

// Legacy Tabulations.aspx/TabulationView.aspx/AssessmentResults.aspx (tracker rows 15-16) - "View
// Reports". Gated on IsViewer alone, exactly like legacy (ExternalUserService now grants the
// Viewer role to every resolved Participant/Test Consultant too - see its own comments - so this
// single-role check already covers all three legacy-visible roles). Every lookup is further scoped
// to the caller's own access by PTL.Api's "for-user" tabulation endpoints
// (ParticipantTabulationService), never by trusting a bare id from the URL.
[Authorize(Policy = ExternalPolicies.Viewer)]
public sealed partial class TabulationController(
    IDistributionApiClient distributionApiClient,
    ITabulationPdfService tabulationPdfService,
    ILogger<TabulationController> logger) : Controller
{
    private static readonly string[] ValidSortColumns = ["reference", "schemeCode", "schemeName", "distributionDate"];
    private static readonly string[] ValidSortDirections = ["asc", "desc"];

    // Legacy Tabulations.aspx - Year/Month dropdowns are exact matches, the 3 text criteria are
    // "contains" (Scheme Name matches when every typed word is contained), mirroring
    // DistributionController.Previous's DistributionsPast.ExecuteSearch pattern. Legacy has no
    // default grid sort: rows come in spgTabulations order (Distribution Reference ascending) with
    // no column marked as sorted, so SortBy stays empty until the user picks a column.
    public async Task<IActionResult> Index([FromQuery] ReferenceSchemeListQuery query, CancellationToken cancellationToken = default)
    {
        var sortBy = query.SortBy is not null && ValidSortColumns.Contains(query.SortBy) ? query.SortBy : string.Empty;
        var sortDir = sortBy.Length > 0 && query.SortDir is not null && ValidSortDirections.Contains(query.SortDir) ? query.SortDir : "asc";
        var page = Math.Max(query.Page, 1);
        var pageSize = PaginationModel.AvailablePageSizes.Contains(query.PageSize) ? query.PageSize : ReferenceSchemeListQuery.DefaultPageSize;

        var (participantId, viewerId, testConsultantId) = CallerIds();

        var list = await distributionApiClient.GetTabulationsForUserAsync(participantId, viewerId, testConsultantId, cancellationToken);

        // Legacy Tabulations.aspx.vb's PopulateDropdowns builds the Year dropdown from both
        // YearId and DistributionDate.Year across the user's own full (unfiltered) tabulation list.
        var availableYears = list.Tabulations
            .SelectMany(t => new[] { t.YearId, t.DistributionDate.Year })
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        var filtered = Filter(list.Tabulations, query.Year, query.Month, query.DistributionReference, query.SchemeCode, query.SchemeName);
        var sorted = Sort(filtered, sortBy, sortDir);
        var paged = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return View(new TabulationListViewModel(
            paged, sorted.Count, page, pageSize, sortBy, sortDir, query.Year, query.Month, query.DistributionReference, query.SchemeCode, query.SchemeName, availableYears));
    }

    // Legacy TabulationView.aspx - read only, no participant/viewer switcher (unlike InternalWeb's
    // staff-facing Tabulation/PublishedTabulation, this is always the signed-in user's own report).
    public async Task<IActionResult> Report(Guid publishedTabulationId, string? returnUrl, CancellationToken cancellationToken)
    {
        var (participantId, viewerId, testConsultantId) = CallerIds();
        var report = await distributionApiClient.GetPublishedTabulationForUserAsync(
            publishedTabulationId, participantId, viewerId, testConsultantId, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        var listUrl = ListReturnUrl.Validate(returnUrl, ListReturnUrl.TabulationIndex);
        ViewData["ListReturnUrl"] = listUrl;
        return View(new TabulationReportPageViewModel(report, listUrl, TabulationAccordionBuilder.Build(report)));
    }

    // Legacy AssessmentResults.aspx - rendered as a data table (see AssessmentResultsPageViewModel).
    public async Task<IActionResult> AssessmentResults(Guid publishedTabulationId, Guid testId, string? returnUrl, CancellationToken cancellationToken)
    {
        var (participantId, viewerId, testConsultantId) = CallerIds();
        var results = await distributionApiClient.GetAssessmentResultsForUserAsync(
            publishedTabulationId, testId, participantId, viewerId, testConsultantId, cancellationToken);
        if (results is null)
        {
            return NotFound();
        }

        var listUrl = ListReturnUrl.Validate(returnUrl, ListReturnUrl.TabulationIndex);
        ViewData["ListReturnUrl"] = listUrl;
        return View(new AssessmentResultsPageViewModel(publishedTabulationId, testId, results, listUrl));
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

    [HttpGet]
    public async Task<IActionResult> GeneratePdf(Guid publishedTabulationId, CancellationToken cancellationToken)
    {
        var (participantId, viewerId, testConsultantId) = CallerIds();
        var report = await distributionApiClient.GetPublishedTabulationForUserAsync(
            publishedTabulationId, participantId, viewerId, testConsultantId, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        try
        {
            var model = new TabulationReportPageViewModel(report);
            var pdf = await tabulationPdfService.GenerateAsync(ControllerContext, model, cancellationToken);
            Response.Headers.ContentDisposition = $"inline; filename=\"{BuildPdfFileName(report)}\"";
            return File(pdf, "application/pdf");
        }
        catch (Exception exception) when (exception is PlaywrightException or InvalidOperationException)
        {
            LogPdfGenerationFailed(exception, publishedTabulationId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to generate tabulation PDF for published tabulation {PublishedTabulationId}.")]
    private partial void LogPdfGenerationFailed(Exception exception, Guid publishedTabulationId);

    private (Guid? ParticipantId, Guid? ViewerId, Guid? TestConsultantId) CallerIds() =>
        (User.GetParticipantId(), User.GetViewerId(), User.GetTestConsultantId());

    // Legacy ShowPrintableTabulation names the file {Distribution}_{SchemeName}_{DistributionDate}.pdf.
    private static string BuildPdfFileName(TabulationReportResponse report)
    {
        var heading = report.DistributionHeading.Trim();
        var space = heading.IndexOf(' ');
        var dateSeparator = report.DistributionDateText.LastIndexOf(": ", StringComparison.Ordinal);
        var date = dateSeparator < 0 ? report.DistributionDateText : report.DistributionDateText[(dateSeparator + 2)..];

        var name = space < 0 || date.Length == 0
            ? report.TabulationName
            : $"{heading[..space]}_{heading[(space + 1)..]}_{date}";

        var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "tabulation-report.pdf" : $"{safe}.pdf";
    }

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

        var ordered = sortBy switch
        {
            "schemeCode" => Order(t => t.SchemeCode),
            "schemeName" => Order(t => t.SchemeName),
            "distributionDate" => Order(t => t.DistributionDate),
            _ => Order(t => t.DistributionReference),
        };

        return [.. ordered];
    }
}
