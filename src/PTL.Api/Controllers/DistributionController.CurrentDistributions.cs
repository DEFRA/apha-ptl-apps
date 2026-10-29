using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Controllers;

// External Enter Results current distributions list (legacy ExternalWeb/DistributionsCurrent.aspx).
public sealed partial class DistributionController
{
    [HttpGet("participants/{participantId:guid}/current")]
    [ProducesResponseType<IReadOnlyList<CurrentDistributionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CurrentDistributionResponse>>> GetCurrentDistributions(
        Guid participantId,
        [FromServices] ICurrentDistributionsService currentDistributionsService,
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
