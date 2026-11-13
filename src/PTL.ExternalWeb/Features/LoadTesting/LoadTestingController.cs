using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PTL.Auth.Cidm.Claims;
using PTL.Auth.Cidm.Events;
using PTL.Contracts.LoadTesting;
using PTL.ExternalWeb.Features.Lab;

namespace PTL.ExternalWeb.Features.LoadTesting;

/// <summary>
/// Email-only sign-in for JMeter runs. Uses the contact id PTL.Api already stores for the email and resolves it through the same
/// <see cref="ICidmExternalUserResolver"/> as a real CIDM sign-in, so the session carries identical claims and no user record is
/// written. 404 unless <see cref="LoadTestingOptions.Enabled"/>.
/// </summary>
[AllowAnonymous]
[SkipLabSelection]
public sealed class LoadTestingController(
    IOptions<LoadTestingOptions> options,
    ILoadTestingLookupClient lookupClient,
    ICidmExternalUserResolver resolver) : Controller
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

        var email = model.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
        {
            return Fail(model, StatusCodes.Status400BadRequest, "Enter an email address.");
        }

        var lookup = await lookupClient.LookupAsync(email, cancellationToken);
        if (lookup is null)
        {
            return Fail(model, StatusCodes.Status503ServiceUnavailable, "PTL.Api has no load-testing lookup; deploy the PTL.Api build that includes it.");
        }

        if (lookup.Status != LoadTestingLookupStatus.Found || lookup.SsoIdExt is not { } contactId)
        {
            return Fail(model, StatusCodes.Status403Forbidden, lookup.Status switch
            {
                LoadTestingLookupStatus.NotLinked => "This user has no stored contact id (fldSsoIdExt); it must be set in the database first.",
                LoadTestingLookupStatus.Ambiguous => "This email's records carry more than one contact id.",
                _ => "No PTL external user has this email."
            });
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(CidmClaimTypes.ContactId, contactId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim("name", email)
        ]));

        var resolution = await resolver.ResolveAsync(principal, cancellationToken);
        if (!resolution.IsAllowed)
        {
            return Fail(model, StatusCodes.Status403Forbidden, "This user cannot sign in (inactive, no active lab, or not resolved).");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, contactId.ToString()),
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Email, email),
            new(CidmClaimTypes.ContactId, contactId.ToString())
        };
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
    public string? Email { get; set; }

    public string? ReturnUrl { get; set; }

    public string? Error { get; set; }
}
