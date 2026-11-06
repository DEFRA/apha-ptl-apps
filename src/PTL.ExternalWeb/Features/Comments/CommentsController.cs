using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Contracts.Distribution;
using PTL.ExternalWeb.Authorization;
using PTL.SharedUI.Notifications;

namespace PTL.ExternalWeb.Features.Comments;

// Legacy DistributionComments.aspx/CommentsEntry.aspx - Test Consultant-only list + comment entry.
[Authorize(Policy = ExternalPolicies.TestConsultant)]
public sealed class CommentsController(ITestConsultantTabulationApiClient apiClient) : Controller
{
    private const string SubmitCommand = "submit";

    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        var distributions = await apiClient.GetDistributionsAsync(cancellationToken);

        var rows = string.IsNullOrWhiteSpace(search)
            ? distributions.Distributions
            : [.. distributions.Distributions.Where(d => d.DistributionTitle.Contains(search, StringComparison.OrdinalIgnoreCase))];

        return View(new CommentsListViewModel(rows, search));
    }

    [HttpGet]
    public async Task<IActionResult> Entry(Guid schemeId, CancellationToken cancellationToken)
    {
        var tabulation = await apiClient.GetTabulationAsync(schemeId, TestConsultantTabulationFilter.None, cancellationToken);
        if (!tabulation.IsOk || tabulation.Value is null)
        {
            return NotOk(tabulation.Status);
        }

        return View(CommentsEntryViewModel.From(schemeId, tabulation.Value));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entry(Guid schemeId, CommentsEntryViewModel model, string? command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.Equals(command, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index));
        }

        var latest = await apiClient.GetTabulationAsync(schemeId, TestConsultantTabulationFilter.None, cancellationToken);
        if (!latest.IsOk || latest.Value is null)
        {
            return NotOk(latest.Status);
        }

        if (latest.Value.Comments.IsSubmitted)
        {
            ModelState.AddModelError(string.Empty, "These comments have already been submitted and can no longer be edited.");
            return View(CommentsEntryViewModel.From(schemeId, latest.Value, model.Comments));
        }

        var submit = string.Equals(command, SubmitCommand, StringComparison.OrdinalIgnoreCase);
        if (submit && !model.Acknowledged)
        {
            ModelState.AddModelError(nameof(CommentsEntryViewModel.Acknowledged), "Select to confirm you understand submitted comments cannot be edited.");
            return View(CommentsEntryViewModel.From(schemeId, latest.Value, model.Comments));
        }

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
            if (saved.Value.IsLocked)
            {
                ModelState.AddModelError(string.Empty, "These comments have already been submitted and can no longer be edited.");
            }
            else
            {
                foreach (var (key, message) in saved.Value.Errors)
                {
                    ModelState.AddModelError(key == "Comments" ? nameof(CommentsEntryViewModel.Comments) : string.Empty, message);
                }
            }

            return View(CommentsEntryViewModel.From(schemeId, latest.Value, model.Comments));
        }

        TempData.SetNotification(NotificationType.Success, submit ? "Comments submitted successfully." : "Comments saved successfully.");
        return RedirectToAction(nameof(Index));
    }

    private IActionResult NotOk(TestConsultantApiStatus status) =>
        status == TestConsultantApiStatus.Forbidden ? Forbid() : NotFound();
}
