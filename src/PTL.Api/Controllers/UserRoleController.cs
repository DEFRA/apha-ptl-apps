using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.User;
using PTL.Core.User;
using ContractsSetUserRolesItemResult = PTL.Contracts.User.SetUserRolesItemResult;

namespace PTL.Api.Controllers;

// Split out of UserController (Oct 2026) - this depends only on IUserRoleService, giving the
// role-assignment/self-removal business rules their own controller while keeping the same
// api/users route prefix, since roles/removal remain a sub-resource of the User aggregate.
[ApiController]
[Route("api/users")]
public sealed class UserRoleController(IUserRoleService userRoleService) : ControllerBase
{
    // Items/RoleIds counts are capped BEFORE any dictionary/hash-set work is done in the service,
    // so an oversized or malicious payload (e.g. millions of rows, or a single row with millions
    // of role ids) is rejected with 400 instead of causing unbounded server-side memory
    // allocation - the service itself trusts its caller's input size. RequestSizeLimit on
    // SetUserRolesBatch below rejects an oversized body before JSON deserialization even runs.
    private const int MaxBatchItems = 2_000;
    private const int MaxRoleIdsPerItem = 500;

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

    // Saves every row shown on the Manage User Roles grid in a single request/transaction,
    // instead of one PUT per user - avoids the intermittent gateway timeouts that the per-row
    // loop caused on larger grids (each row re-queried all users/roles and opened its own
    // transaction).
    [HttpPut("roles")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<SetUserRolesBatchResponse>> SetUserRolesBatch([FromBody] SetUserRolesBatchRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count > MaxBatchItems)
        {
            ModelState.AddModelError(nameof(request.Items), $"No more than {MaxBatchItems} rows can be saved in a single request.");
            return ValidationProblem(ModelState);
        }

        if (request.Items.Any(i => i.RoleIds.Count > MaxRoleIdsPerItem))
        {
            ModelState.AddModelError(nameof(SetUserRolesBatchItem.RoleIds), $"No more than {MaxRoleIdsPerItem} role ids can be submitted for a single user.");
            return ValidationProblem(ModelState);
        }

        var rows = request.Items.Select(i => (i.UserId, (IReadOnlyList<Guid>)i.RoleIds)).ToList();
        var results = await userRoleService.SetUserRolesBatchAsync(rows, request.ActingUserId, cancellationToken);
        return Ok(new SetUserRolesBatchResponse(results.Select(r => new ContractsSetUserRolesItemResult(r.UserId, r.Success, r.Message)).ToList()));
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
