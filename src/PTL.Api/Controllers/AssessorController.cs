using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.Core.Audit;
using PTL.Core.Distribution;

namespace PTL.Api.Controllers;

// Assessor menu and assessment items (legacy Assessor/MenuAssessor.aspx and AssessmentItemList.aspx). The Assessor is the acting
// user PTL.InternalWeb forwards (see ActingUserDelegatingHandler); a request without one, or for a test whose scheme the user is
// not an Assessor of, is refused with 403.
// [NEEDS INVESTIGATION] Not yet protected with authorization, like the other API controllers - see UserController's note.
[ApiController]
[Route("api/assessor")]
public sealed class AssessorController(IAssessorService service, IActingUserAccessor actingUser) : ControllerBase
{
    // GET /api/assessor/menu?year= - the years with distributions to assess and the months of the chosen one (the latest by default).
    [HttpGet("menu")]
    [ProducesResponseType<AssessorMenuResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AssessorMenuResponse>> GetMenu(int? year, CancellationToken cancellationToken)
    {
        if (actingUser.ActingUserId is not { } assessorId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return Ok(await service.GetMenuAsync(assessorId, year, cancellationToken));
    }

    // GET /api/assessor/tests/{testId}/items - the participant submissions of one test, a table per sample.
    [HttpGet("tests/{testId:guid}/items")]
    [ProducesResponseType<AssessorItemListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssessorItemListResponse>> GetItemList(Guid testId, CancellationToken cancellationToken)
    {
        if (actingUser.ActingUserId is not { } assessorId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await service.GetItemListAsync(assessorId, testId, cancellationToken);
        return result.Access switch
        {
            AssessorAccess.Granted => Ok(result.Value),
            AssessorAccess.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            _ => NotFound(),
        };
    }

    // POST /api/assessor/tests/{testId}/submit - Submit. 204 when submitted, 403 for a test the user does not assess, 404 for an
    // unknown test, 409 when it has already been submitted.
    [HttpPost("tests/{testId:guid}/submit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitAssessment(Guid testId, CancellationToken cancellationToken)
    {
        if (actingUser.ActingUserId is not { } assessorId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await service.SubmitAsync(assessorId, testId, cancellationToken) switch
        {
            AssessorSubmitOutcome.Submitted => NoContent(),
            AssessorSubmitOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            AssessorSubmitOutcome.AlreadySubmitted => Conflict(),
            _ => NotFound(),
        };
    }

    // GET /api/assessor/entries/{sampleTestId}/{participantId} - one participant's submission of one sample, with the test's
    // categories and criteria and what has been saved. ParticipantId is the distribution participant (see the item list).
    [HttpGet("entries/{sampleTestId:guid}/{participantId:guid}")]
    [ProducesResponseType<AssessorEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssessorEntryResponse>> GetEntry(Guid sampleTestId, Guid participantId, CancellationToken cancellationToken)
    {
        if (actingUser.ActingUserId is not { } assessorId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await service.GetEntryAsync(assessorId, sampleTestId, participantId, cancellationToken);
        return result.Access switch
        {
            AssessorAccess.Granted => Ok(result.Value),
            AssessorAccess.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            _ => NotFound(),
        };
    }

    // PUT /api/assessor/entries/{sampleTestId}/{participantId} - Save and Apply. 204 when saved (also when nothing changed),
    // 400 with the fields at fault, 403 for a test the user does not assess, 404 for an unknown submission, 409 once the test
    // has been submitted.
    [HttpPut("entries/{sampleTestId:guid}/{participantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AssessorValidationResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveEntry(Guid sampleTestId, Guid participantId, AssessorEntrySaveRequest request, CancellationToken cancellationToken)
    {
        if (actingUser.ActingUserId is not { } assessorId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await service.SaveEntryAsync(assessorId, sampleTestId, participantId, request, cancellationToken);
        return result.Outcome switch
        {
            AssessorSaveOutcome.Saved => NoContent(),
            AssessorSaveOutcome.Invalid => BadRequest(new AssessorValidationResponse([.. result.Errors.Select(e => new AssessorValidationErrorResponse(e.Key, e.Message))])),
            AssessorSaveOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            AssessorSaveOutcome.Submitted => Conflict(),
            _ => NotFound(),
        };
    }
}
