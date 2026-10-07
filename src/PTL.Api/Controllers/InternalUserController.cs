using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.InternalUser;
using PTL.Core.InternalUser;

namespace PTL.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class InternalUserController(IInternalUserService internalUserService) : ControllerBase
{
    /// <summary>
    /// Resolves an Entra ID-authenticated internal user against tblUsers. Unlike external-user
    /// resolution, there is no "limited access" allowance - a user with no matching row is denied.
    /// </summary>
    [HttpPost("internal-users/resolve")]
    public async Task<ActionResult<ResolveInternalUserResponse>> ResolveInternalUser(
        [FromBody] ResolveInternalUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await internalUserService.ResolveAsync(request.SsoIdInt, request.Username, cancellationToken);

        if (!result.IsPermitted || result.User is null)
        {
            return Ok(new ResolveInternalUserResponse(false, null, string.Empty, string.Empty, []));
        }

        var user = result.User;
        var fullName = !string.IsNullOrWhiteSpace(user.FriendlyName)
            ? user.FriendlyName
            : $"{user.FirstName} {user.LastName}".Trim();

        return Ok(new ResolveInternalUserResponse(true, user.UserId, fullName, user.Department, user.Roles));
    }
}
