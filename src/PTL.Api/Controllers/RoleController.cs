using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.User;
using PTL.Core.User;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization - see UserController's note.
[ApiController]
[Route("api/roles")]
public sealed class RoleController(IUserRoleService userRoleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await userRoleService.GetRolesAsync(cancellationToken);
        return Ok(roles.Select(r => new RoleResponse(r.RoleId, r.Name)).ToList());
    }
}
