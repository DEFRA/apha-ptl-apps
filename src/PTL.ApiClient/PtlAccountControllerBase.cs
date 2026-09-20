using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace PTL.ApiClient;

/// <summary>
/// Shared cookie sign-in/sign-out mechanics for the PTL web front-ends' Account controllers -
/// identical for each app except where a successful login redirects to, which the derived
/// controller supplies via <see cref="PostLoginRedirectController"/>.
/// </summary>
public abstract class PtlAccountControllerBase : Controller
{
    protected abstract string PostLoginRedirectController { get; }

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
