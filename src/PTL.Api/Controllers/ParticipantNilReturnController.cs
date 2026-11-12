using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Controllers;

// Shares the api/distributions prefix so the Nil Return URLs are unchanged.
[ApiController]
[Route("api/distributions")]
public sealed class ParticipantNilReturnController(IParticipantNilReturnService nilReturnService) : ControllerBase
{
    [HttpGet("schemes/{monthlyDistributionSchemeId:guid}/participants/{distributionParticipantId:guid}/nil-return")]
    [ProducesResponseType<ParticipantNilReturnResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParticipantNilReturnResponse>> GetParticipantNilReturn(
        Guid monthlyDistributionSchemeId,
        Guid distributionParticipantId,
        CancellationToken cancellationToken)
    {
        var data = await nilReturnService.GetAsync(distributionParticipantId, monthlyDistributionSchemeId, cancellationToken);
        return data is null
            ? NotFound()
            : Ok(new ParticipantNilReturnResponse(!string.IsNullOrWhiteSpace(data.NilReturnReason), data.NilReturnReason));
    }

    [HttpPut("schemes/{monthlyDistributionSchemeId:guid}/participants/{distributionParticipantId:guid}/nil-return")]
    [ProducesResponseType<ParticipantNilReturnSaveResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParticipantNilReturnSaveResult>> SaveParticipantNilReturn(
        Guid monthlyDistributionSchemeId,
        Guid distributionParticipantId,
        [FromBody] ParticipantNilReturnSaveRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await nilReturnService.SaveAsync(
            distributionParticipantId,
            monthlyDistributionSchemeId,
            request.NilReturnReason,
            cancellationToken);

        if (outcome.Status == ParticipantNilReturnSaveStatus.NotFound)
        {
            return NotFound();
        }

        return Ok(new ParticipantNilReturnSaveResult(
            outcome.Success,
            DeadlinePassed: outcome.Status == ParticipantNilReturnSaveStatus.DeadlinePassed,
            ReasonRequired: outcome.Status is ParticipantNilReturnSaveStatus.ReasonRequired or ParticipantNilReturnSaveStatus.TooLong,
            HasResults: outcome.Status == ParticipantNilReturnSaveStatus.HasResults));
    }
}
