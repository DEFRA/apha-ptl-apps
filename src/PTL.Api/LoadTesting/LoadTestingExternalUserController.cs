using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.LoadTesting;
using PTL.Core.ExternalUser;

namespace PTL.Api.LoadTesting;

/// <summary>
/// Read-only lookup of the contact id already stored for an external user's email, so the load-testing sign-in can use the
/// user's existing link instead of writing one. 404 unless the caller sends <see cref="LoadTestingHeaders.Enabled"/>, which
/// PTL.ExternalWeb does only while its own switch is on, so PTL.Api needs no setting of its own.
/// </summary>
[ApiController]
[Route("api/load-testing")]
public sealed class LoadTestingExternalUserController(IExternalUserRepository repository) : ControllerBase
{
    [HttpPost("external-users/lookup")]
    public async Task<ActionResult<LoadTestingLookupResponse>> Lookup(
        [FromBody] LoadTestingLookupRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.Headers[LoadTestingHeaders.Enabled] != "true")
        {
            return NotFound();
        }

        var email = request.Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(request.Email)] = ["The email is required."] }));
        }

        var matches = await repository.FindByEmailAsync(email, cancellationToken);
        if (matches.Count == 0)
        {
            return Ok(new LoadTestingLookupResponse(LoadTestingLookupStatus.NotFound, null));
        }

        // Same priority as sign-in: only the lowest user type counts.
        var lowestType = matches.Min(match => match.UserType);
        var linkedIds = matches
            .Where(match => match.UserType == lowestType && match.SsoIdExt is not null)
            .Select(match => match.SsoIdExt!.Value)
            .Distinct()
            .ToList();

        return Ok(linkedIds.Count switch
        {
            0 => new LoadTestingLookupResponse(LoadTestingLookupStatus.NotLinked, null),
            1 => new LoadTestingLookupResponse(LoadTestingLookupStatus.Found, linkedIds[0]),
            _ => new LoadTestingLookupResponse(LoadTestingLookupStatus.Ambiguous, null)
        });
    }
}
