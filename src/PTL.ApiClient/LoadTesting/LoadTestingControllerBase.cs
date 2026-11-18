using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PTL.Common.Auth;

namespace PTL.ApiClient;

/// <summary>
/// Shared mechanics of the identity-provider-free sign-in used by JMeter runs: 404 unless <see cref="LoadTestingOptions.Enabled"/>,
/// an antiforgery-protected POST, and a cookie sign-in carrying the claims the app's own resolver returns, so no user record is
/// written. The derived controller supplies how its user is looked up and resolved.
/// </summary>
[AllowAnonymous]
[Route("perf")]
public abstract class LoadTestingControllerBase<TModel> : Controller
    where TModel : LoadTestingLoginViewModelBase, new()
{
    protected const string ApiUnavailableError = "PTL.Api has no load-testing lookup; deploy the PTL.Api build that includes it.";

    private readonly IOptions<LoadTestingOptions> _options;

    protected LoadTestingControllerBase(IOptions<LoadTestingOptions> options) => _options = options;

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) =>
        _options.Value.Enabled ? View(new TModel { ReturnUrl = returnUrl }) : NotFound();

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(TModel model, CancellationToken cancellationToken) =>
        _options.Value.Enabled ? await SignInAsync(model, cancellationToken) : NotFound();

    /// <summary>Validates the entered user, looks them up and resolves them, then calls <see cref="CompleteSignInAsync"/> or <see cref="Fail"/>.</summary>
    protected abstract Task<IActionResult> SignInAsync(TModel model, CancellationToken cancellationToken);

    /// <summary>Signs in with <paramref name="claims"/> plus the resolver's claims, or refuses with <paramref name="deniedError"/>.</summary>
    protected async Task<IActionResult> CompleteSignInAsync(TModel model, IIdentityResolution resolution, List<Claim> claims, string deniedError)
    {
        if (!resolution.IsAllowed)
        {
            return Fail(model, StatusCodes.Status403Forbidden, deniedError);
        }

        claims.AddRange((resolution.Claims ?? new Dictionary<string, string>()).Select(claim => new Claim(claim.Key, claim.Value)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl)
            : RedirectToAction("Index", "Home");
    }

    protected ViewResult Fail(TModel model, int statusCode, string error)
    {
        Response.StatusCode = statusCode;
        model.Error = error;
        return View(model);
    }
}

public abstract class LoadTestingLoginViewModelBase
{
    public string? ReturnUrl { get; set; }

    public string? Error { get; set; }
}
