using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.TestConsultant;
using PTL.Core.TestConsultant;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as the other System Administration controllers.
//
// Generate Login is a stub - see IExternalLoginService remarks. The story's Developer Note flags
// this whole function's future as subject to a business decision (CIDM/GOV.UK One Login may
// replace it with a "Send Invitation" capability instead).
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
        var updated = await testConsultantService.SetStatusAsync(externalTestConsultantId, request.IsInactive, cancellationToken);
        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    // Returns 200 OK with Success=false (not 400/404) when generation fails - a legitimate
    // outcome (not found, or the stub/future integration reports failure), matching
    // CountryController.DeleteCountry.
    [HttpPost("{externalTestConsultantId:guid}/generate-login")]
    public async Task<ActionResult<GenerateLoginResponse>> GenerateLogin(Guid externalTestConsultantId, CancellationToken cancellationToken)
    {
        var (success, message) = await testConsultantService.GenerateLoginAsync(externalTestConsultantId, cancellationToken);
        return Ok(new GenerateLoginResponse(success, message));
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
            testConsultant.InactiveDate,
            testConsultant.SsoId != Guid.Empty);
}
