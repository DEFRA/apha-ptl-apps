using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.TestConsultant;
using PTL.Core.TestConsultant;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as the other System Administration controllers.
[ApiController]
[Route("api/external-test-consultants")]
public sealed class ExternalTestConsultantController(ITestConsultantService testConsultantService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExternalTestConsultantResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var consultants = await testConsultantService.GetAllAsync(cancellationToken);
        return Ok(consultants.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ExternalTestConsultantResponse>> Create([FromBody] ExternalTestConsultantSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await testConsultantService.CreateAsync(request.Name, request.Department, request.Email, cancellationToken);
            return Ok(ToResponse(created));
        }
        catch (ExternalTestConsultantValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("{externalTestConsultantId:guid}")]
    public async Task<ActionResult<ExternalTestConsultantResponse>> Update(Guid externalTestConsultantId, [FromBody] ExternalTestConsultantSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await testConsultantService.UpdateAsync(externalTestConsultantId, request.Name, request.Department, request.Email, cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (ExternalTestConsultantValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("{externalTestConsultantId:guid}/status")]
    public async Task<ActionResult<ExternalTestConsultantResponse>> SetStatus(Guid externalTestConsultantId, [FromBody] ExternalTestConsultantStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await testConsultantService.SetStatusAsync(externalTestConsultantId, request.IsInactive, cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (ExternalTestConsultantValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    // Returns 200 OK with Success=false (not 404/409) when removal is refused - a legitimate
    // outcome (already gone, or still allocated to schemes), matching ViewerController.Delete.
    [HttpDelete("{externalTestConsultantId:guid}")]
    public async Task<ActionResult<ExternalTestConsultantDeleteResponse>> Delete(Guid externalTestConsultantId, CancellationToken cancellationToken)
    {
        var result = await testConsultantService.DeleteAsync(externalTestConsultantId, cancellationToken);
        return Ok(new ExternalTestConsultantDeleteResponse(result.Success, result.Message));
    }

    private ActionResult ToValidationProblem(ExternalTestConsultantValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static ExternalTestConsultantResponse ToResponse(PTL.Core.TestConsultant.TestConsultant testConsultant) =>
        new(
            testConsultant.ExternalTestConsultantId,
            testConsultant.Name,
            testConsultant.Department,
            testConsultant.Email,
            testConsultant.IsInactive,
            testConsultant.InactiveDate);
}
