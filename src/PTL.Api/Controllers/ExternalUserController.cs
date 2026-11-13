using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.ExternalUser;
using PTL.Core.ExternalUser;

namespace PTL.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ExternalUserController(IExternalUserService externalUserService) : ControllerBase
{
    /// <summary>
    /// Resolves a CIDM-authenticated external user to a single PTL user type (Participant, Viewer, Test
    /// Consultant or Lab User) by CIDM contact id, linking the contact id to the matching record by email the
    /// first time. The role comes from the matched record, never from the request. No record is ever created.
    /// </summary>
    [HttpPost("external-users/resolve")]
    public async Task<ActionResult<ResolveExternalUserResponse>> ResolveExternalUser(
        [FromBody] ResolveExternalUserRequest request,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(errors));
        }

        var result = await externalUserService.ResolveAsync(
            request.SsoIdExt,
            request.Email,
            request.DisplayName,
            cancellationToken);

        return Ok(new ResolveExternalUserResponse(
            result.Status,
            result.DisplayName,
            result.Roles ?? [],
            result.Labs ?? [],
            result.UserType,
            result.ParticipantId,
            result.LabCode,
            result.ViewerId,
            result.TestConsultantId,
            result.LabUserId,
            result.CanOrderOnline,
            result.CanEditDetails));
    }

    /// <summary>The active labs a signed-in Participant or Lab User can work on (empty for any other user), for the lab selection page.</summary>
    [HttpGet("external-users/{ssoIdExt:guid}/labs")]
    public async Task<ActionResult<IReadOnlyList<ExternalLab>>> GetLabs(Guid ssoIdExt, CancellationToken cancellationToken) =>
        Ok(await externalUserService.GetLabsAsync(ssoIdExt, cancellationToken));

    /// <summary>
    /// The chosen lab, only when it is one of the user's active labs. Anything else is 404 whether the lab does not exist or
    /// belongs to someone else, so a lab id cannot be probed.
    /// </summary>
    [HttpGet("external-users/{ssoIdExt:guid}/labs/{participantId:guid}")]
    public async Task<ActionResult<ExternalLabContext>> GetLab(Guid ssoIdExt, Guid participantId, CancellationToken cancellationToken)
    {
        var lab = await externalUserService.GetLabAsync(ssoIdExt, participantId, cancellationToken);
        return lab is null ? NotFound() : Ok(lab);
    }

    private static Dictionary<string, string[]> Validate(ResolveExternalUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.SsoIdExt == Guid.Empty)
        {
            errors[nameof(request.SsoIdExt)] = ["The CIDM contact id is required."];
        }

        if (request.Email.Length > ResolveExternalUserRequest.MaxEmailLength)
        {
            errors[nameof(request.Email)] = [$"The email must be at most {ResolveExternalUserRequest.MaxEmailLength} characters."];
        }

        if (request.DisplayName.Length > ResolveExternalUserRequest.MaxDisplayNameLength)
        {
            errors[nameof(request.DisplayName)] = [$"The display name must be at most {ResolveExternalUserRequest.MaxDisplayNameLength} characters."];
        }

        return errors;
    }
}
