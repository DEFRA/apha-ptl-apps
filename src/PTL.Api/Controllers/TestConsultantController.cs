using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Distribution;
using PTL.Core.Audit;
using PTL.Core.Distribution;

namespace PTL.Api.Controllers;

// Test Consultant menu, tabulation and comments (legacy Test Consultant/MenuTestConsultants.aspx and Tabulation.aspx).
// The Test Consultant is the acting user PTL.InternalWeb forwards (see ActingUserDelegatingHandler); a request without
// one, or for a scheme the user is not allocated to, is refused with 403.
[ApiController]
[Route("api/test-consultant")]
public sealed class TestConsultantController(ITestConsultantTabulationService service, IActingUserAccessor actingUser) : ControllerBase
{
    [HttpGet("distributions")]
    [ProducesResponseType<TestConsultantDistributionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TestConsultantDistributionsResponse>> GetDistributions(CancellationToken cancellationToken)
    {
        if (actingUser.ActingUserId is not { } testConsultantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await service.GetDistributionsAsync(testConsultantId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpGet("schemes/{monthlyDistributionSchemeId:guid}/tabulation")]
    [ProducesResponseType<TestConsultantTabulationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestConsultantTabulationResponse>> GetTabulation(
        Guid monthlyDistributionSchemeId, [FromQuery] string[]? hiddenSamples, [FromQuery] bool hideMethodItems, CancellationToken cancellationToken)
    {
        if (actingUser.ActingUserId is not { } testConsultantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await service.GetTabulationAsync(
            testConsultantId, monthlyDistributionSchemeId, new TestConsultantTabulationFilter(hiddenSamples ?? [], hideMethodItems), cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpGet("schemes/{monthlyDistributionSchemeId:guid}/tabulation/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportTabulation(
        Guid monthlyDistributionSchemeId, [FromQuery] string[]? hiddenSamples, [FromQuery] bool hideMethodItems, CancellationToken cancellationToken)
    {
        if (actingUser.ActingUserId is not { } testConsultantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await service.ExportAsync(
            testConsultantId, monthlyDistributionSchemeId, new TestConsultantTabulationFilter(hiddenSamples ?? [], hideMethodItems), cancellationToken);
        return ToActionResult(result, file => File(file.Content, TabulationWorkbookWriter.ContentType, file.FileName));
    }

    [HttpPut("schemes/{monthlyDistributionSchemeId:guid}/comments")]
    [ProducesResponseType<TestConsultantTabulationSaveResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestConsultantTabulationSaveResult>> SaveComments(
        Guid monthlyDistributionSchemeId, TestConsultantTabulationSaveRequest request, CancellationToken cancellationToken)
    {
        if (actingUser.ActingUserId is not { } testConsultantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await service.SaveCommentsAsync(testConsultantId, monthlyDistributionSchemeId, request, cancellationToken);
        return ToActionResult(result, Ok);
    }

    private ActionResult ToActionResult<T>(TestConsultantResult<T> result, Func<T, ActionResult> onGranted) =>
        result.Access switch
        {
            TestConsultantAccess.Granted => onGranted(result.Value!),
            TestConsultantAccess.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            _ => NotFound(),
        };
}
