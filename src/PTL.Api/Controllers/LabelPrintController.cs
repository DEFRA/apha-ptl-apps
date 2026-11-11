using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Labels;
using PTL.Core.Labels;

namespace PTL.Api.Controllers;

// Printer interaction is centralised here, not in every web front-end - PTL.Api is the one place
// with the printer's network location configured (LabelPrinterOptions), and the one place that
// needs to change if that location or protocol ever does.
[ApiController]
[Route("api/labels")]
public sealed class LabelPrintController(ILabelPrinterClient labelPrinterClient) : ControllerBase
{
    [HttpPost("print")]
    public async Task<ActionResult<LabelPrintResponse>> Print([FromBody] LabelPrintRequest request, CancellationToken cancellationToken)
    {
        var printed = await labelPrinterClient.PrintAsync(new AddressLabel(request.Lines), cancellationToken);
        return Ok(new LabelPrintResponse(printed));
    }
}
