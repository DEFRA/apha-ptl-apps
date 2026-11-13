using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PTL.Auth.Entra.Claims;
using PTL.Auth.Entra.Events;
using PTL.Contracts.LoadTesting;

namespace PTL.InternalWeb.Features.LoadTesting;

/// <summary>
/// Username-only sign-in for JMeter runs. Uses the directory object id PTL.Api already stores for the username and resolves it
/// through the same <see cref="IEntraInternalUserResolver"/> as a real Entra sign-in, so the session carries identical claims and
/// no user record is written. 404 unless <see cref="LoadTestingOptions.Enabled"/>.
/// </summary>
[AllowAnonymous]
public sealed class LoadTestingController(
    IOptions<LoadTestingOptions> options,
    ILoadTestingLookupClient lookupClient,
    IEntraInternalUserResolver resolver) : Controller
{
    [HttpGet("perf/login")]
    public IActionResult Login(string? returnUrl = null) =>
        options.Value.Enabled ? View(new LoadTestingLoginViewModel { ReturnUrl = returnUrl }) : NotFound();

    // No antiforgery token so a JMeter script can sign in with a single POST.
    [HttpPost("perf/login")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Login(LoadTestingLoginViewModel model, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        var username = model.Username?.Trim();
        var separator = username?.IndexOf('\\') ?? -1;
        if (string.IsNullOrEmpty(username) || separator <= 0 || separator == username.Length - 1)
        {
            return Fail(model, StatusCodes.Status400BadRequest, "Enter the username as DOMAIN\\name, for example DEFRA\\jsmith.");
        }

        var lookup = await lookupClient.LookupAsync(username, cancellationToken);
        if (lookup is null)
        {
            return Fail(model, StatusCodes.Status503ServiceUnavailable, "PTL.Api has no load-testing lookup; deploy the PTL.Api build that includes it.");
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
        if (!resolution.IsAllowed)
        {
            return Fail(model, StatusCodes.Status403Forbidden, "This user cannot sign in (not resolved).");
        }

        claims.AddRange((resolution.Claims ?? new Dictionary<string, string>()).Select(claim => new Claim(claim.Key, claim.Value)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl)
            : RedirectToAction("Index", "Home");
    }

    private ViewResult Fail(LoadTestingLoginViewModel model, int statusCode, string error)
    {
        Response.StatusCode = statusCode;
        model.Error = error;
        return View(model);
    }
}

public sealed class LoadTestingLoginViewModel
{
    public string? Username { get; set; }

    public string? ReturnUrl { get; set; }

    public string? Error { get; set; }
}
