using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PTL.ApiClient;
using PTL.Auth.Entra.Claims;
using PTL.Auth.Entra.Events;
using PTL.Contracts.LoadTesting;

namespace PTL.InternalWeb.Features.LoadTesting;

/// <summary>
/// Username-only sign-in for JMeter runs. Uses the directory object id PTL.Api already stores for the username and resolves it
/// through the same <see cref="IEntraInternalUserResolver"/> as a real Entra sign-in, so the session carries identical claims and
/// no user record is written. 404 unless <see cref="LoadTestingOptions.IsAvailable"/>.
/// </summary>
public sealed class LoadTestingController(
    IOptions<LoadTestingOptions> options,
    TimeProvider timeProvider,
    ILoadTestingLookupClient lookupClient,
    IEntraInternalUserResolver resolver) : LoadTestingControllerBase<LoadTestingLoginViewModel>(options, timeProvider)
{
    protected override async Task<IActionResult> SignInAsync(LoadTestingLoginViewModel model, CancellationToken cancellationToken)
    {
        var username = model.Username?.Trim();
        var separator = username?.IndexOf('\\') ?? -1;
        if (string.IsNullOrEmpty(username) || separator <= 0 || separator == username.Length - 1)
        {
            return Fail(model, StatusCodes.Status400BadRequest, "Enter the username as DOMAIN\\name, for example DEFRA\\jsmith.");
        }

        var lookup = await lookupClient.LookupInternalUserAsync(username, cancellationToken);
        if (lookup is null)
        {
            return Fail(model, StatusCodes.Status503ServiceUnavailable, ApiUnavailableError);
        }

        if (lookup.Status != LoadTestingInternalLookupStatus.Found || lookup.SsoIdInt is not { } ssoIdInt)
        {
            return Fail(model, StatusCodes.Status403Forbidden, lookup.Status == LoadTestingInternalLookupStatus.NotLinked
                ? "This user has no stored object id (fldSsoIdInt); it must be set in the database first."
                : "No PTL internal user has this username.");
        }

        // The on-premises claims are kept so the periodic role re-check can resolve the user again without calling the directory.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, ssoIdInt.ToString()),
            new(EntraClaimTypes.ObjectId, ssoIdInt.ToString()),
            new(EntraClaimTypes.OnPremisesDomainName, username[..separator]),
            new(EntraClaimTypes.OnPremisesSamAccountName, username[(separator + 1)..])
        };

        var resolution = await resolver.ResolveAsync(new ClaimsPrincipal(new ClaimsIdentity(claims)), cancellationToken);
        return await CompleteSignInAsync(model, resolution, claims, "This user cannot sign in (not resolved).");
    }
}

public sealed class LoadTestingLoginViewModel : LoadTestingLoginViewModelBase
{
    public string? Username { get; set; }
}
