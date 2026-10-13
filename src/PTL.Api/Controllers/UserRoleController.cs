using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.User;
using PTL.Core.User;

namespace PTL.Api.Controllers;

// Split out of UserController (Oct 2026) - this depends only on IUserRoleService, giving the
// role-assignment/self-removal business rules their own controller while keeping the same
// api/users route prefix, since roles/removal remain a sub-resource of the User aggregate.
[ApiController]
[Route("api/users")]
public sealed class UserRoleController(IUserRoleService userRoleService) : ControllerBase
{
    // GET /api/users/roles - the Manage User Roles grid (every user plus their assigned role ids).
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<UserRoleRowResponse>>> GetUserRoles(CancellationToken cancellationToken)
    {
        var rows = await userRoleService.GetUserRoleGridAsync(cancellationToken);
        return Ok(rows.Select(r => new UserRoleRowResponse(r.UserId, r.Username, r.FriendlyName, r.RoleIds)).ToList());
    }

    // Returns 200 OK with Success=false (not 400) when blocked by a business rule (e.g. removing
    // your own Admin role) - a legitimate outcome, matching CountryController.DeleteCountry.
    [HttpPut("{userId:guid}/roles")]
    public async Task<ActionResult<SetUserRolesResponse>> SetUserRoles(Guid userId, [FromBody] SetUserRolesRequest request, CancellationToken cancellationToken)
    {
        var result = await userRoleService.SetUserRolesAsync(userId, request.RoleIds, request.ActingUserId, cancellationToken);
        return Ok(new SetUserRolesResponse(result.Success, result.Message));
    }

    // Returns 200 OK with Success=false (not 400) when removal is blocked by a business rule
    // (e.g. removing your own account) - matches CountryController.DeleteCountry. actingUserId is
    // the signed-in admin performing the removal (resolved by PTL.InternalWeb from Entra claims).
    [HttpDelete("{userId:guid}")]
    public async Task<ActionResult<UserRemoveResponse>> RemoveUser(Guid userId, [FromQuery] Guid? actingUserId, CancellationToken cancellationToken)
    {
        var result = await userRoleService.RemoveUserAsync(userId, actingUserId, cancellationToken);
        return Ok(new UserRemoveResponse(result.Success, result.Message));
    }
}
