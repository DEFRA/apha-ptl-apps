using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Controllers;

// Amend Results (legacy Scheduling/AmendResults.aspx).
public sealed partial class DistributionController
{
    [HttpGet("schemes/{monthlyDistributionSchemeId:guid}/amend-results")]
    [ProducesResponseType<AmendResultsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AmendResultsResponse>> GetAmendResults(
        Guid monthlyDistributionSchemeId, [FromServices] IAmendResultsService amendResultsService, CancellationToken cancellationToken)
    {
        var page = await amendResultsService.GetAsync(monthlyDistributionSchemeId, cancellationToken);
        return page is null ? NotFound() : Ok(page);
    }

    [HttpPut("schemes/{monthlyDistributionSchemeId:guid}/amend-results")]
    [ProducesResponseType<AmendResultsSaveResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AmendResultsSaveResult>> SaveAmendResults(
        Guid monthlyDistributionSchemeId,
        AmendResultsSaveRequest request,
        [FromServices] IAmendResultsService amendResultsService,
        CancellationToken cancellationToken)
    {
        var result = await amendResultsService.SaveAsync(monthlyDistributionSchemeId, request, AuditWho(), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
