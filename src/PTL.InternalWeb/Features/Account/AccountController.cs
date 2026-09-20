using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PTL.InternalWeb.Features.Account
{
    public class AccountController : Controller
    {
        // Reached automatically by the authorization fallback policy whenever an unauthenticated
        // request hits any page, and also usable directly as an explicit "Sign in" link target.
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            var redirectUri = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
            return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, OpenIdConnectDefaults.AuthenticationScheme);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            // Sign out of both the local cookie and Entra ID (front-channel logout), otherwise the
            // browser's still-live Entra ID session would silently re-establish a new local session
            // on the very next request via SSO.
            var authProperties = new AuthenticationProperties { RedirectUri = Url.Action("Index", "Home") };
            return SignOut(authProperties, CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            return View(new AccessDeniedViewModel { ReturnUrl = returnUrl });
        }
    }
}

