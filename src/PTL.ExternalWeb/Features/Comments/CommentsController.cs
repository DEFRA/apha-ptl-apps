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
    private const string CommentsLockedMessage = "These comments have already been submitted and can no longer be edited.";
    private const string AcknowledgementRequiredMessage = "Select to confirm you understand submitted comments cannot be edited.";

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

        if (!ValidateEditableState(model, latest.Value, command))
        {
            return View(CommentsEntryViewModel.From(schemeId, latest.Value, model.Comments));
        }

        if (!ModelState.IsValid)
        {
            return View(CommentsEntryViewModel.From(schemeId, latest.Value, model.Comments));
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
            return View(CommentsEntryViewModel.From(schemeId, latest.Value, model.Comments));
        }

        TempData.SetNotification(NotificationType.Success, submit ? "Comments submitted successfully." : "Comments saved successfully.");
        return RedirectToAction(nameof(Index));
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

    private IActionResult NotOk(TestConsultantApiStatus status) =>
        status == TestConsultantApiStatus.Forbidden ? Forbid() : NotFound();
}
