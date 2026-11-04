using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Controllers;

// External Enter Results current distributions list (legacy ExternalWeb/DistributionsCurrent.aspx).
[ApiController]
[Route("api/distributions/participants")]
public sealed class CurrentDistributionsController(ICurrentDistributionsService currentDistributionsService) : ControllerBase
{
    [HttpGet("{participantId:guid}/current")]
    [ProducesResponseType<IReadOnlyList<CurrentDistributionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CurrentDistributionResponse>>> GetCurrentDistributions(
        Guid participantId,
        CancellationToken cancellationToken)
    {
        var distributions = await currentDistributionsService.GetCurrentDistributionsAsync(participantId, cancellationToken);

        return Ok(distributions.Select(d => new CurrentDistributionResponse(
            d.MonthlyDistributionSchemeId,
            d.DistributionParticipantId,
            d.DistributionReference,
            d.SchemeCode,
            d.SchemeName,
            d.DistributionDate,
            d.DeadlineDate,
            d.HasResults,
            d.Submitted)).ToList());
    }
}
