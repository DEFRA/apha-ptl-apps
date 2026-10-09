using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.User;
using PTL.Core.User;

namespace PTL.Api.Controllers;

// [NEEDS INVESTIGATION] Not yet protected with authorization: authentication/authorization are
// out of scope for this phase, same as the other System Administration controllers. This one
// matters more than most, though - legacy's Admin role is granted via this same domain's
// (not-yet-built) Assign Roles screen, so this entire domain is self-administering and must not
// go live without the authorization policy decision described in
// docs/migration/System-Administration-migration.md.
//
// The directory-search endpoint is backed by a stub (PTL.Core.User.StubStaffDirectoryService) -
// see IStaffDirectoryService for why a real Active Directory/Entra ID integration isn't wired up yet.
[ApiController]
[Route("api/users")]
public sealed class UserController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await userService.GetAllAsync(cancellationToken);
        return Ok(users.Select(ToResponse).ToList());
    }

    [HttpGet("directory-search")]
    public async Task<ActionResult<IReadOnlyList<StaffDirectoryUserResponse>>> SearchDirectory([FromQuery] string searchTerm, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Ok(Array.Empty<StaffDirectoryUserResponse>());
        }

        var results = await userService.SearchStaffDirectoryAsync(searchTerm, cancellationToken);
        return Ok(results.Select(r => new StaffDirectoryUserResponse(r.Username, r.Email, r.FriendlyName, r.FirstName, r.LastName)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await userService.CreateUserAsync(request.Username, request.Email, request.FriendlyName, request.FirstName, request.LastName, request.Department, cancellationToken);
            return Ok(ToResponse(created));
        }
        catch (UserValidationException ex)
        {
            foreach (var error in ex.Errors)
            {
                ModelState.AddModelError(error.Field, error.Message);
            }

            return ValidationProblem(ModelState);
        }
    }

    // GET /api/users/test-consultants - the Internal Test Consultant Department Management grid.
    [HttpGet("test-consultants")]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetTestConsultants(CancellationToken cancellationToken)
    {
        var users = await userService.GetTestConsultantsAsync(cancellationToken);
        return Ok(users.Select(ToResponse).ToList());
    }

    [HttpPut("{userId:guid}/test-consultant")]
    public async Task<IActionResult> UpdateTestConsultant(Guid userId, [FromBody] UpdateTestConsultantRequest request, CancellationToken cancellationToken)
    {
        await userService.UpdateTestConsultantAsync(userId, request.Department, request.IsInactive, request.InactiveDate, cancellationToken);
        return Ok();
    }

    private static UserResponse ToResponse(PTL.Core.User.User user) =>
        new(user.UserId, user.Username, user.FriendlyName, user.FirstName, user.LastName, user.Email, user.Department, user.IsInactive, user.InactiveDate);
}

