using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PTL.ApiClient;

/// <summary>
/// Shared cookie sign-in/sign-out mechanics for the PTL web front-ends' Account controllers -
/// identical for each app except where a successful login redirects to, which the derived
/// controller supplies via <see cref="PostLoginRedirectController"/>.
/// </summary>
public abstract class PtlAccountControllerBase<T> : Controller where T : class, IAccountCredentials
{
    protected abstract string PostLoginRedirectController { get; }

    protected abstract T CreateLoginModel(string? returnUrl = null);

    [HttpGet]
    [AllowAnonymous]
    public virtual IActionResult Login(string? returnUrl = null)
    {
        var model = CreateLoginModel(returnUrl);
        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public virtual async Task<IActionResult> Login(T model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(string.Empty, "Please provide username and password.");
            return View(model ?? CreateLoginModel());
        }

        return await SignInAndRedirectAsync(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public virtual Task<IActionResult> Logout() => SignOutAndRedirectToHomeAsync();

    /// <summary>
    /// Called by session-timeout.js when the user chooses to stay signed in. Reaching this [Authorize]-by-default
    /// action at all (cookie validation and any token refresh already ran in the pipeline) proves the session is still
    /// valid and slides the cookie; a failed or redirected response tells the script to show Session expired instead.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExtendSession() => NoContent();

    /// <summary>
    /// Reached when the inactivity countdown elapses with no action. Signs out of this app's own cookie only (not the
    /// identity provider's sign-out), so a live single sign-on session signs the user straight back in on the next
    /// sign-in. Signed out unconditionally so a stale principal can never slip through; NoStore so a cached copy can
    /// never mask the sign-out that just happened.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> SessionExpired()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // SignOutAsync only affects later requests; clear the user for this response so the header does not
        // still show "Signed in as" on the page that says the session has ended.
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        return View();
    }

    /// <summary>
    /// Redirects to an external identity provider's OIDC challenge - shared by every PTL web
    /// front-end that signs in via an external IdP (CIDM for external users, Entra ID for internal
    /// users) instead of this base class's default username/password flow.
    /// </summary>
    protected IActionResult ChallengeExternalLogin(string authenticationScheme, string? returnUrl)
    {
        var redirectUri = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Action("Index", PostLoginRedirectController);

        return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, authenticationScheme);
    }

    /// <summary>
    /// Signs out of the cookie scheme then redirects to the external identity provider's own
    /// sign-out endpoint, carrying the captured id_token_hint so the IdP honours
    /// post_logout_redirect_uri instead of showing its own default sign-out page. Captured BEFORE
    /// clearing the cookie (OpenIdConnectHandler.HandleSignOutAsync only reads id_token_hint from the
    /// AuthenticationProperties passed to SignOut() or falls back to re-authenticating against the
    /// cookie scheme, which finds nothing once the cookie has already been cleared). Signed out
    /// separately (not via a single SignOut(cookie, oidc) call sharing one AuthenticationProperties)
    /// since CookieAuthenticationHandler.SignOutAsync also honours RedirectUri and would otherwise
    /// hijack the response before the OIDC scheme's sign-out event renders the IdP's sign-out form.
    /// </summary>
    protected async Task<IActionResult> SignOutViaExternalSchemeAsync(string authenticationScheme)
    {
        var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var idToken = cookieResult?.Properties?.GetTokenValue("id_token");

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var properties = new AuthenticationProperties { RedirectUri = Url.Action("SignedOut", "Account") };
        if (!string.IsNullOrEmpty(idToken))
        {
            properties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
        }

        return SignOut(properties, authenticationScheme);
    }

    protected async Task<IActionResult> SignInAndRedirectAsync(IAccountCredentials model)
    {
        var claims = new[] { new Claim(ClaimTypes.Name, model.Username!) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var authProperties = new AuthenticationProperties { IsPersistent = false };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", PostLoginRedirectController);
    }

    protected async Task<IActionResult> SignOutAndRedirectToHomeAsync()
    {
        TempData["LoginMessage"] = "Signed out";
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
}

public static class ModelStateExtensions
{
    public static void AddFieldErrors(this ControllerBase controller, IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        foreach (var (field, messages) in fieldErrors)
        {
            foreach (var message in messages)
            {
                controller.ModelState.AddModelError(field, message);
            }
        }
    }
}
