using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.LoadTesting;
using PTL.Core.InternalUser;

namespace PTL.Api.LoadTesting;

/// <summary>
/// Read-only lookup of the directory object id already stored for an internal user's username, so the load-testing sign-in can
/// reuse it instead of letting sign-in write one. 404 unless the caller sends <see cref="LoadTestingHeaders.Enabled"/>.
/// </summary>
[ApiController]
[Route("api/load-testing")]
public sealed class LoadTestingInternalUserController(IInternalUserRepository repository) : ControllerBase
{
    [HttpPost("internal-users/lookup")]
    public async Task<ActionResult<LoadTestingInternalLookupResponse>> Lookup(
        [FromBody] LoadTestingInternalLookupRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.Headers[LoadTestingHeaders.Enabled] != "true")
        {
            return NotFound();
        }

        var username = request.Username?.Trim();
        if (string.IsNullOrEmpty(username))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(request.Username)] = ["The username is required."] }));
        }

        var user = await repository.GetByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            return Ok(new LoadTestingInternalLookupResponse(LoadTestingInternalLookupStatus.NotFound, null));
        }

        return Ok(user.SsoIdInt is { } ssoIdInt
            ? new LoadTestingInternalLookupResponse(LoadTestingInternalLookupStatus.Found, ssoIdInt)
            : new LoadTestingInternalLookupResponse(LoadTestingInternalLookupStatus.NotLinked, null));
    }
}
