using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient.Distribution;
using PTL.InternalWeb.Authorization;
using PTL.InternalWeb.Confirmation;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.ResultsSignOff;

// Results Sign-off - legacy Results Sign-off/SignOff.aspx (the queue), Tabulation.aspx (the review, with Authorise and Reject) and
// AssessmentResults.aspx, open only to the Results Sign-off role. The review reads the draft tabulation the Distribution pages
// already load. Authorise publishes it and Reject returns it for amendments.
[Authorize(Policy = PtlPolicies.ResultsSignOff)]
[Route("Results Sign-off")]
public sealed class ResultsSignOffController(IResultsSignOffApiClient apiClient, IDistributionApiClient distributionApiClient) : Controller
{
    // Legacy BtnReject and BtnAuthorise had no confirmation; the shared confirmation page asks before a decision that cannot be undone.
    public const string RejectQuestion = "Are you sure you wish to Reject this Tabulation?";
    public const string RejectMessage = "Rejected Tabulations are returned for amendments and restart the tabulation cycle.";
    public const string RejectedNotification = "The Tabulation has been rejected.";
    public const string AuthoriseQuestion = "Are you sure you wish to Authorise this Tabulation?";
    public const string AuthoriseMessage = "Authorising publishes the Tabulation and sends its Tabulation Published notification emails.";
    public const string AuthorisedNotification = "The Tabulation has been authorised and published.";
    public const string NoLongerAwaitingNotification = "This Tabulation is no longer awaiting Sign-off.";
    public const string NoSignatoryNotification = "The Tabulation could not be authorised because you are not signed in as a PT-LIMS user.";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(ResultsSignOffQueueViewModel.From(await apiClient.GetQueueAsync(cancellationToken)));

    [HttpGet("Tabulation")]
    public async Task<IActionResult> Tabulation(Guid tabulationId, Guid? participantId, Guid? viewerId, [FromQuery] ResultsSignOffDraw draw, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draw);

        var review = await apiClient.GetReviewAsync(tabulationId, participantId ?? Guid.Empty, viewerId ?? Guid.Empty, draw.ToFilter(), cancellationToken);
        if (review is null)
        {
            return NotFound();
        }

        draw.Display(review.SampleNumbers);
        return View(new ResultsSignOffReviewViewModel { TabulationId = tabulationId, Review = review, Draw = draw });
    }

    [HttpGet("AssessmentResults")]
    public async Task<IActionResult> AssessmentResults(Guid tabulationId, Guid testId, CancellationToken cancellationToken)
    {
        var results = await distributionApiClient.GetAssessmentResultsAsync(tabulationId, testId, false, cancellationToken);
        if (results is null)
        {
            return NotFound();
        }

        return View(new ResultsSignOffAssessmentViewModel { TabulationId = tabulationId, Results = results });
    }

    // Legacy AttachmentDisplay "View Supporting Document".
    [HttpGet("Attachment")]
    public async Task<IActionResult> Attachment(Guid tabulationId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var file = await distributionApiClient.GetTabulationAttachmentAsync(tabulationId, false, attachmentId, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        Response.Headers.ContentDisposition = file.ContentDisposition;
        return File(file.Content, file.ContentType);
    }

    [HttpPost("Authorise")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Authorise(Guid tabulationId, string? confirmation, CancellationToken cancellationToken)
    {
        if (!ConfirmationFlow.IsConfirmed(confirmation))
        {
            return this.ConfirmationPage(
                new ConfirmationPageViewModel
                {
                    Title = AuthoriseQuestion,
                    PageTitle = "Authorise Tabulation",
                    Message = AuthoriseMessage,
                    ConfirmText = "Authorise",
                    CancelUrl = Url.Action(nameof(Tabulation), new { tabulationId }),
                },
                nameof(Tabulation));
        }

        switch (await apiClient.AuthoriseAsync(tabulationId, cancellationToken))
        {
            case ResultsSignOffApiStatus.NotFound:
                return NotFound();
            case ResultsSignOffApiStatus.NotAwaiting:
                TempData.SetNotification(NotificationType.Error, NoLongerAwaitingNotification);
                break;
            case ResultsSignOffApiStatus.NoSignatory:
                TempData.SetNotification(NotificationType.Error, NoSignatoryNotification);
                break;
            default:
                TempData.SetNotification(NotificationType.Success, AuthorisedNotification);
                break;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid tabulationId, string? confirmation, CancellationToken cancellationToken)
    {
        if (!ConfirmationFlow.IsConfirmed(confirmation))
        {
            return this.ConfirmationPage(
                new ConfirmationPageViewModel
                {
                    Title = RejectQuestion,
                    PageTitle = "Reject Tabulation",
                    Message = RejectMessage,
                    ConfirmText = "Reject",
                    IsDestructive = true,
                    CancelUrl = Url.Action(nameof(Tabulation), new { tabulationId }),
                },
                nameof(Tabulation));
        }

        switch (await apiClient.RejectAsync(tabulationId, cancellationToken))
        {
            case ResultsSignOffApiStatus.NotFound:
                return NotFound();
            case ResultsSignOffApiStatus.NotAwaiting:
                TempData.SetNotification(NotificationType.Error, NoLongerAwaitingNotification);
                break;
            default:
                TempData.SetNotification(NotificationType.Success, RejectedNotification);
                break;
        }

        return RedirectToAction(nameof(Index));
    }
}
