using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Controllers;

// Tabulations "Send" actions (legacy Scheduling/Tabulations.aspx GridViewMDS_RowCommand + Save).
// Sends are staged by the page and only written by the PUT (legacy Save).
public sealed partial class DistributionController
{
    [HttpPost("months/{yearId:int}/{monthId:int}/tabulations/preview")]
    [ProducesResponseType<TabulationsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TabulationsResponse>> PreviewTabulations(
        int yearId, int monthId, TabulationsStagedRequest request, [FromServices] ITabulationSendService tabulationSendService, CancellationToken cancellationToken)
    {
        var page = await tabulationSendService.PreviewAsync(yearId, monthId, request?.Staged ?? [], cancellationToken);
        return page is null ? NotFound() : Ok(ToResponse(page));
    }

    [HttpPost("months/{yearId:int}/{monthId:int}/tabulations/stage")]
    [ProducesResponseType<TabulationSendResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TabulationSendResponse>> StageTabulationSend(
        int yearId, int monthId, TabulationStageRequest request, [FromServices] ITabulationSendService tabulationSendService, CancellationToken cancellationToken)
    {
        var send = await tabulationSendService.StageAsync(yearId, monthId, request, cancellationToken);
        return send is null ? NotFound() : Ok(send);
    }

    // 409 when a staged send is no longer available (e.g. the scheme was changed by someone else).
    [HttpPut("months/{yearId:int}/{monthId:int}/tabulations")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveTabulations(
        int yearId, int monthId, TabulationsStagedRequest request, [FromServices] ITabulationSendService tabulationSendService, CancellationToken cancellationToken) =>
        await tabulationSendService.SaveAsync(yearId, monthId, request?.Staged ?? [], cancellationToken)
            ? NoContent()
            : Conflict();
}
