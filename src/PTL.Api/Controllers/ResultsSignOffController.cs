using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.Core.Audit;
using PTL.Core.Distribution;

namespace PTL.Api.Controllers;

// Legacy Results Sign-off/SignOff.aspx. Who may open the queue (the Results Sign-off role) is enforced by PTL.InternalWeb.
// [NEEDS INVESTIGATION] Not yet protected with authorization, like the other API controllers - see UserController's note.
[ApiController]
[Route("api/results-sign-off")]
public sealed class ResultsSignOffController(IResultsSignOffService service, ITabulationReportService tabulationReportService, IActingUserAccessor actingUser) : ControllerBase
{
    // GET /api/results-sign-off/queue - the tabulations awaiting final approval, grouped by month and scheme.
    [HttpGet("queue")]
    [ProducesResponseType<ResultsSignOffQueueResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultsSignOffQueueResponse>> GetQueue(CancellationToken cancellationToken) =>
        Ok(await service.GetQueueAsync(cancellationToken));

    // GET /api/results-sign-off/tabulations/{id} - the draft tabulation for review, without the sample columns listed in
    // hiddenSamples (and without the method items when hideMethodItems is set). 404 for an unknown tabulation.
    [HttpGet("tabulations/{tabulationId:guid}")]
    [ProducesResponseType<ResultsSignOffReviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultsSignOffReviewResponse>> GetReview(
        Guid tabulationId, Guid participantId, Guid viewerId, [FromQuery] string[]? hiddenSamples, bool hideMethodItems, CancellationToken cancellationToken)
    {
        var review = await tabulationReportService.GetSignOffReviewAsync(
            tabulationId, participantId, viewerId, new TestConsultantTabulationFilter(hiddenSamples ?? [], hideMethodItems), cancellationToken);
        return review is null ? NotFound() : Ok(review);
    }

    // POST /api/results-sign-off/tabulations/{id}/reject - 204 when rejected, 404 for an unknown tabulation, 409 when it is
    // no longer awaiting sign-off.
    [HttpPost("tabulations/{tabulationId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid tabulationId, CancellationToken cancellationToken) =>
        await service.RejectAsync(tabulationId, cancellationToken) switch
        {
            ResultsSignOffOutcome.Done => NoContent(),
            ResultsSignOffOutcome.NotFound => NotFound(),
            _ => Conflict(),
        };

    // POST /api/results-sign-off/tabulations/{id}/authorise - publishes the tabulation, signed by the acting user (the user
    // PTL.InternalWeb forwards, see ActingUserDelegatingHandler). 204 when published, 403 without an acting user, 404 for an
    // unknown tabulation, 409 when it is no longer awaiting sign-off.
    [HttpPost("tabulations/{tabulationId:guid}/authorise")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Authorise(Guid tabulationId, CancellationToken cancellationToken) =>
        await service.AuthoriseAsync(tabulationId, actingUser.ActingUserId, cancellationToken) switch
        {
            ResultsSignOffOutcome.Done => NoContent(),
            ResultsSignOffOutcome.NotFound => NotFound(),
            ResultsSignOffOutcome.NoSignatory => StatusCode(StatusCodes.Status403Forbidden),
            _ => Conflict(),
        };
}
