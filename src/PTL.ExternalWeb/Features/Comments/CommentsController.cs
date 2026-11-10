using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Authorization;
using PTL.SharedUI.Notifications;
using PTL.ExternalWeb.ViewSupport;

namespace PTL.ExternalWeb.Features.Comments;

// Legacy DistributionComments.aspx/CommentsEntry.aspx - Test Consultant-only list + comment entry.
[Authorize(Policy = ExternalPolicies.TestConsultant)]
public sealed class CommentsController(ITestConsultantTabulationApiClient apiClient) : Controller
{
    private const string DrawCommand = "draw";
    private const string SubmitCommand = "submit";
    private const string CommentsLockedMessage = "These comments have already been submitted and can no longer be edited.";
    private const string AcknowledgementRequiredMessage = "Select to confirm you understand submitted comments cannot be edited.";

    private static readonly string[] ValidSortColumns = ["reference", "schemeCode", "schemeName", "targetDate", "commentsProvidedBy", "submitted"];
    private static readonly string[] ValidSortDirections = ["asc", "desc"];

    public async Task<IActionResult> Index(
        string? distributionReference,
        string? schemeCode,
        string? schemeName,
        string? sortBy,
        string? sortDir,
        CancellationToken cancellationToken)
    {
        var distributions = await apiClient.GetDistributionsAsync(cancellationToken);

        var rows = distributions.Distributions
            .Select(MapRow)
            .Where(d =>
                Contains(d.DistributionReference, distributionReference)
                && Contains(d.SchemeCode, schemeCode)
                && ContainsAllWords(d.SchemeName, schemeName))
            .ToList();

        rows = await PopulateMissingCommentsProvidedByAsync(rows, cancellationToken);

        var column = sortBy is not null && ValidSortColumns.Contains(sortBy) ? sortBy : "targetDate";
        var direction = sortDir is not null && ValidSortDirections.Contains(sortDir) ? sortDir : "desc";
        var sorted = Sort(rows, column, direction);

        return View(new CommentsListViewModel(sorted, distributionReference, schemeCode, schemeName, column, direction));
    }

    [HttpGet]
    public async Task<IActionResult> Entry(Guid schemeId, string? returnUrl, CancellationToken cancellationToken)
    {
        var tabulation = await apiClient.GetTabulationAsync(schemeId, TestConsultantTabulationFilter.None, cancellationToken);
        if (!tabulation.IsOk || tabulation.Value is null)
        {
            return NotOk(tabulation.Status);
        }

        var model = CommentsEntryViewModel.From(schemeId, tabulation.Value);
        model.ReturnUrl = ListReturnUrl.Validate(returnUrl, ListReturnUrl.CommentsIndex);
        ViewData["ListReturnUrl"] = model.ReturnUrl;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entry(Guid schemeId, CommentsEntryViewModel model, string? command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.ReturnUrl = ListReturnUrl.Validate(model.ReturnUrl, ListReturnUrl.CommentsIndex);

        if (string.Equals(command, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToIndex(model.ReturnUrl);
        }

        var isDraw = string.Equals(command, DrawCommand, StringComparison.OrdinalIgnoreCase);
        if (isDraw)
        {
            ModelState.Remove(nameof(CommentsEntryViewModel.Comments));
            ModelState.Remove(nameof(CommentsEntryViewModel.Acknowledged));
        }

        var latest = await apiClient.GetTabulationAsync(
            schemeId,
            isDraw ? model.ToFilter() : TestConsultantTabulationFilter.None,
            cancellationToken);
        if (!latest.IsOk || latest.Value is null)
        {
            return NotOk(latest.Status);
        }

        if (isDraw)
        {
            return EntryView(schemeId, latest.Value, model);
        }

        if (!ValidateEditableState(model, latest.Value, command))
        {
            return EntryView(schemeId, latest.Value, model);
        }

        if (!ModelState.IsValid)
        {
            return EntryView(schemeId, latest.Value, model);
        }

        var submit = string.Equals(command, SubmitCommand, StringComparison.OrdinalIgnoreCase);
        var saved = await apiClient.SaveCommentsAsync(
            schemeId,
            new TestConsultantTabulationSaveRequest(model.Comments, submit),
            cancellationToken);
        if (!saved.IsOk || saved.Value is null)
        {
            return NotOk(saved.Status);
        }

        if (!saved.Value.Success)
        {
            ApplySaveErrors(saved.Value);
            return EntryView(schemeId, latest.Value, model);
        }

        TempData.SetNotification(NotificationType.Success, submit ? "Comments submitted successfully." : "Comments saved successfully.");
        return RedirectToIndex(model.ReturnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(Guid schemeId, CommentsEntryViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await apiClient.ExportTabulationAsync(schemeId, model.ToFilter(), cancellationToken);
        if (!result.IsOk || result.Value is null)
        {
            return NotOk(result.Status);
        }

        Response.Headers.ContentDisposition = result.Value.ContentDisposition;
        return File(result.Value.Content, result.Value.ContentType);
    }

    private bool ValidateEditableState(
        CommentsEntryViewModel model,
        TestConsultantTabulationResponse latest,
        string? command)
    {
        if (latest.Comments.IsSubmitted)
        {
            ModelState.AddModelError(string.Empty, CommentsLockedMessage);
            return false;
        }

        var submit = string.Equals(command, SubmitCommand, StringComparison.OrdinalIgnoreCase);
        if (submit && !model.Acknowledged)
        {
            ModelState.AddModelError(nameof(CommentsEntryViewModel.Acknowledged), AcknowledgementRequiredMessage);
            return false;
        }

        return true;
    }

    private void ApplySaveErrors(TestConsultantTabulationSaveResult result)
    {
        if (result.IsLocked)
        {
            ModelState.AddModelError(string.Empty, CommentsLockedMessage);
            return;
        }

        foreach (var (key, message) in result.Errors)
        {
            ModelState.AddModelError(key == "Comments" ? nameof(CommentsEntryViewModel.Comments) : string.Empty, message);
        }
    }

    private static CommentsDistributionListRow MapRow(TestConsultantDistributionResponse distribution)
    {
        var parsed = ParseTitle(distribution.DistributionTitle);
        return new CommentsDistributionListRow(
            distribution.MonthlyDistributionSchemeId,
            parsed.DistributionReference,
            parsed.SchemeCode,
            parsed.SchemeName,
            distribution.TargetDate,
            distribution.CommentStatus,
            distribution.CommentsProvidedBy,
            distribution.IsCommentSubmitted);
    }

    private static ParsedDistributionTitle ParseTitle(string title)
    {
        var slash = title.IndexOf('/');
        var colon = title.IndexOf(':');
        var hasSegments = slash > 0 && colon > slash;
        if (!hasSegments)
        {
            return new ParsedDistributionTitle(title, string.Empty, title);
        }

        var distributionReference = title[..slash].Trim();
        var schemeCode = title[(slash + 1)..colon].Trim();
        var schemeName = title[(colon + 1)..].Trim();
        return new ParsedDistributionTitle(distributionReference, schemeCode, schemeName);
    }

    private static bool Contains(string value, string? term) =>
        string.IsNullOrWhiteSpace(term) || value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAllWords(string value, string? term) =>
        string.IsNullOrWhiteSpace(term)
        || term.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => value.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<CommentsDistributionListRow> Sort(IReadOnlyList<CommentsDistributionListRow> rows, string sortBy, string sortDir)
    {
        IOrderedEnumerable<CommentsDistributionListRow> Order<TKey>(Func<CommentsDistributionListRow, TKey> keySelector) =>
            sortDir == "desc" ? rows.OrderByDescending(keySelector) : rows.OrderBy(keySelector);

        var ordered = sortBy switch
        {
            "reference" => Order(d => d.DistributionReference).ThenBy(d => d.TargetDate).ThenBy(d => d.DistributionReference),
            "schemeCode" => Order(d => d.SchemeCode).ThenBy(d => d.TargetDate).ThenBy(d => d.DistributionReference),
            "schemeName" => Order(d => d.SchemeName).ThenBy(d => d.TargetDate).ThenBy(d => d.DistributionReference),
            "targetDate" => sortDir == "desc"
                ? rows.OrderByDescending(d => d.TargetDate).ThenBy(d => d.DistributionReference)
                : rows.OrderBy(d => d.TargetDate).ThenBy(d => d.DistributionReference),
            "commentsProvidedBy" => Order(d => d.CommentsProvidedBy).ThenBy(d => d.TargetDate).ThenBy(d => d.DistributionReference),
            "submitted" => Order(d => d.IsSubmitted).ThenBy(d => d.TargetDate).ThenBy(d => d.DistributionReference),
            _ => rows.OrderByDescending(d => d.TargetDate).ThenBy(d => d.DistributionReference),
        };

        return [.. ordered];
    }

    private async Task<List<CommentsDistributionListRow>> PopulateMissingCommentsProvidedByAsync(
        List<CommentsDistributionListRow> rows,
        CancellationToken cancellationToken)
    {
        var updated = new List<CommentsDistributionListRow>(rows.Count);

        foreach (var row in rows)
        {
            if (row.CommentsProvidedBy.Length > 0)
            {
                updated.Add(row);
                continue;
            }

            var tabulation = await apiClient.GetTabulationAsync(row.MonthlyDistributionSchemeId, TestConsultantTabulationFilter.None, cancellationToken);
            if (!tabulation.IsOk || tabulation.Value is null)
            {
                updated.Add(row);
                continue;
            }

            var heading = tabulation.Value.Comments.Heading;
            const string prefix = "Comments provided by ";
            var providedBy = heading.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? heading[prefix.Length..].Trim().TrimEnd(':')
                : string.Empty;

            updated.Add(row with { CommentsProvidedBy = providedBy });
        }

        return updated;
    }

    private IActionResult EntryView(Guid schemeId, TestConsultantTabulationResponse tabulation, CommentsEntryViewModel previous)
    {
        var viewModel = CommentsEntryViewModel.From(schemeId, tabulation, previous.Comments, previous);
        viewModel.ReturnUrl = ListReturnUrl.Validate(previous.ReturnUrl, ListReturnUrl.CommentsIndex);
        ViewData["ListReturnUrl"] = viewModel.ReturnUrl;
        return View(viewModel);
    }

    private IActionResult RedirectToIndex(string? returnUrl)
    {
        var safeReturnUrl = ListReturnUrl.Validate(returnUrl, ListReturnUrl.CommentsIndex);
        return safeReturnUrl is null ? RedirectToAction(nameof(Index)) : Redirect(safeReturnUrl);
    }

    private IActionResult NotOk(TestConsultantApiStatus status) =>
        status == TestConsultantApiStatus.Forbidden ? Forbid() : NotFound();

    private sealed record ParsedDistributionTitle(string DistributionReference, string SchemeCode, string SchemeName);
}
