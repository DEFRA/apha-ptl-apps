using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient.Distribution;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Confirmation;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.Assessor;

// Assessor menu, assessment items and assessment entry - legacy Assessor/MenuAssessor.aspx, AssessmentItemList.aspx and
// AssessmentEntry.aspx, open only to the Assessor role. Which tests an Assessor may open or change is decided by PTL.Api from
// the signed-in user, so a request for anyone else's test gets the shared Access Denied page. Save returns to the Assessment
// Items; Apply saves and stays on the entry; Cancel (Back once the test is submitted) leaves without saving.
[Authorize(Policy = PtlPolicies.Assessor)]
[Route("Assessor")]
public sealed class AssessorController(IAssessorApiClient apiClient) : Controller
{
    public const string SavedNotification = "The assessment has been saved.";
    public const string SubmittedNotification = "Assessment has already been submitted for this test, so it can no longer be changed.";

    // Legacy BtnSubmit's two confirm() questions, the second with its "Note: ..." lead-in shown as a warning.
    public const string SubmitQuestion = "Are you sure that you want to submit assessment for this test?";
    public const string IncompleteNote = "Note: Assessment has not been entered for all of the samples.";

    [HttpGet("")]
    public async Task<IActionResult> Index(int? year, CancellationToken cancellationToken)
    {
        var result = await apiClient.GetMenuAsync(year, cancellationToken);
        return result.Value is { } menu && result.IsOk
            ? View(AssessorMenuViewModel.From(menu))
            : NotOk(result.Status);
    }

    [HttpGet("AssessmentItemList")]
    public async Task<IActionResult> AssessmentItemList(Guid testId, int? year, CancellationToken cancellationToken)
    {
        var result = await apiClient.GetItemListAsync(testId, cancellationToken);
        return result.Value is { } list && result.IsOk
            ? View(new AssessorItemListViewModel { List = list, Year = year })
            : NotOk(result.Status);
    }

    [HttpGet("AssessmentEntry")]
    public async Task<IActionResult> AssessmentEntry(Guid sampleTestId, Guid participantId, int? year, CancellationToken cancellationToken)
    {
        var result = await apiClient.GetEntryAsync(sampleTestId, participantId, cancellationToken);
        return result.Value is { } entry && result.IsOk
            ? View(AssessorEntryViewModel.From(entry, year))
            : NotOk(result.Status);
    }

    [HttpPost("AssessmentEntry")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssessmentEntry(AssessorEntryViewModel model, string? command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        var loaded = await apiClient.GetEntryAsync(model.SampleTestId, model.ParticipantId, cancellationToken);
        if (loaded.Value is not { } entry || !loaded.IsOk)
        {
            return NotOk(loaded.Status);
        }

        if (entry.IsAssessmentSubmitted)
        {
            return Submitted(entry.TestId, model.Year);
        }

        model.Display(entry);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var saved = await apiClient.SaveEntryAsync(model.SampleTestId, model.ParticipantId, model.ToRequest(), cancellationToken);
        switch (saved.Status)
        {
            case AssessorSaveStatus.Saved:
                TempData.SetNotification(NotificationType.Success, SavedNotification);
                return command == AssessorEntryViewModel.ApplyCommand
                    ? RedirectToAction(nameof(AssessmentEntry), new { sampleTestId = model.SampleTestId, participantId = model.ParticipantId, testId = entry.TestId, year = model.Year })
                    : RedirectToAction(nameof(AssessmentItemList), new { testId = entry.TestId, year = model.Year });
            case AssessorSaveStatus.Invalid:
                foreach (var error in saved.Errors)
                {
                    ModelState.AddModelError(error.Key, error.Message);
                }

                return View(model);
            case AssessorSaveStatus.Submitted:
                return Submitted(entry.TestId, model.Year);
            default:
                return saved.Status == AssessorSaveStatus.Forbidden ? Forbid() : NotFound();
        }
    }

    private RedirectToActionResult Submitted(Guid testId, int? year)
    {
        TempData.SetNotification(NotificationType.Error, SubmittedNotification);
        return RedirectToAction(nameof(AssessmentItemList), new { testId, year });
    }

    // Legacy BtnSubmit confirmed with a browser dialog: the plain question, or the note that not every sample has been assessed.
    // The shared confirmation page asks instead. Once confirmed the Assessment Items page shows who submitted and when.
    [HttpPost("Submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid testId, int? year, string? confirmation, CancellationToken cancellationToken)
    {
        if (!ConfirmationFlow.IsConfirmed(confirmation))
        {
            var result = await apiClient.GetItemListAsync(testId, cancellationToken);
            if (result.Value is not { } list || !result.IsOk)
            {
                return NotOk(result.Status);
            }

            if (list.IsAssessmentSubmitted)
            {
                return Submitted(testId, year);
            }

            return this.ConfirmationPage(
                new ConfirmationPageViewModel
                {
                    Title = SubmitQuestion,
                    PageTitle = "Submit Assessment",
                    WarningText = list.HasCompletedAssessment ? null : IncompleteNote,
                    ConfirmText = "Submit",
                    CancelUrl = Url.Action(nameof(AssessmentItemList), new { testId, year }),
                },
                nameof(AssessmentItemList));
        }

        switch (await apiClient.SubmitAsync(testId, cancellationToken))
        {
            case AssessorSubmitStatus.Forbidden:
                return Forbid();
            case AssessorSubmitStatus.NotFound:
                return NotFound();
            case AssessorSubmitStatus.AlreadySubmitted:
                return Submitted(testId, year);
            default:
                return RedirectToAction(nameof(AssessmentItemList), new { testId, year });
        }
    }

    // Not an Assessor of the scheme: the same refusal as any role-gated page, which the cookie scheme sends to the shared Access
    // Denied page (legacy had one for the whole site).
    private IActionResult NotOk(AssessorApiStatus status)
    {
        if (status == AssessorApiStatus.Forbidden)
        {
            return Forbid();
        }

        return NotFound();
    }
}
