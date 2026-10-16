using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.ExternalUser;
using PTL.Core.ExternalUser;

namespace PTL.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ExternalUserController(IExternalUserService externalUserService) : ControllerBase
{
    /// <summary>
    /// Resolves a CIDM-authenticated external user against the Participant, Viewer and Test
    /// Consultant tables. Roles that don't resolve by SsoIdExt or email are simply omitted from
    /// the response - no record is ever auto-created.
    /// </summary>
    [HttpPost("external-users/resolve")]
    public async Task<ActionResult<ResolveExternalUserResponse>> ResolveExternalUser(
        [FromBody] ResolveExternalUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await externalUserService.ResolveAsync(
            request.SsoIdExt,
            request.Email,
            request.DisplayName,
            request.Roles,
            cancellationToken);

        return Ok(new ResolveExternalUserResponse(
            result.DisplayName,
            result.Roles,
            result.ParticipantId,
            result.LabCode,
            result.ViewerId,
            result.TestConsultantId,
            result.CanOrderOnline));
    }
}
