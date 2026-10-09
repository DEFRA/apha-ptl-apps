using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Viewer;
using PTL.Core.Viewer;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as the other System Administration controllers.
//
// Generate Login is a stub - see IExternalLoginService remarks. The story's Developer Note flags
// this whole function's future as subject to a business decision (CIDM/GOV.UK One Login may
// replace it with a "Send Invitation" capability instead).
[ApiController]
[Route("api/viewers")]
public sealed class ViewerController(IViewerService viewerService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ViewerResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var viewers = await viewerService.GetAllAsync(cancellationToken);
        return Ok(viewers.Select(ToResponse).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ViewerResponse>> Create([FromBody] ViewerSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await viewerService.CreateAsync(request.Name, request.Email, cancellationToken);
            return Ok(ToResponse(created));
        }
        catch (ViewerValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    [HttpPut("{viewerId:guid}")]
    public async Task<ActionResult<ViewerResponse>> Update(Guid viewerId, [FromBody] ViewerSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await viewerService.UpdateAsync(viewerId, request.Name, request.Email, cancellationToken);
            return updated is null ? NotFound() : Ok(ToResponse(updated));
        }
        catch (ViewerValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    // Returns 200 OK with Success=false (not 404) when removal fails - a legitimate outcome
    // (already gone), matching CountryController.DeleteCountry.
    [HttpDelete("{viewerId:guid}")]
    public async Task<ActionResult<ViewerDeleteResponse>> Delete(Guid viewerId, CancellationToken cancellationToken)
    {
        var result = await viewerService.DeleteAsync(viewerId, cancellationToken);
        return Ok(new ViewerDeleteResponse(result.Success, result.Message));
    }

    [HttpPost("{viewerId:guid}/generate-login")]
    public async Task<ActionResult<PTL.Contracts.TestConsultant.GenerateLoginResponse>> GenerateLogin(Guid viewerId, CancellationToken cancellationToken)
    {
        var (success, message) = await viewerService.GenerateLoginAsync(viewerId, cancellationToken);
        return Ok(new PTL.Contracts.TestConsultant.GenerateLoginResponse(success, message));
    }

    private ActionResult ToValidationProblem(ViewerValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.Field, error.Message);
        }

        return ValidationProblem(ModelState);
    }

    private static ViewerResponse ToResponse(ViewerEntity viewer) =>
        new(
            viewer.ViewerId,
            viewer.Name,
            viewer.Email,
            viewer.SsoId != Guid.Empty,
            viewer.Schemes.Select(s => new ViewerSchemeResponse(s.Identifier, s.Name)).ToList(),
            viewer.Participants.Select(p => new ViewerParticipantResponse(p.LabCode, p.LabName)).ToList());
}
