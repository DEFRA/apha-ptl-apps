using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Auth.Entra;

namespace PTL.InternalWeb.Features.Account
{
    // Sign-in for this app is entirely via Microsoft Entra ID - the base class's username/password
    // form is a stub shared with PTL.ExternalWeb only, and is not used here.
    public class AccountController : PtlAccountControllerBase<AccountViewModel>
    {
        protected override string PostLoginRedirectController => "Home";

        protected override AccountViewModel CreateLoginModel(string? returnUrl = null) =>
            new() { ReturnUrl = returnUrl };

        [HttpGet]
        public override IActionResult Login(string? returnUrl = null)
        {
            var redirectUri = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : Url.Action("Index", PostLoginRedirectController);

            return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, EntraAuthenticationDefaults.AuthenticationScheme);
        }

        // The username/password form this posts to is never rendered for this app (Login(GET) above
        // always challenges Entra ID instead), so this path should be unreachable.
        [HttpPost]
        public override Task<IActionResult> Login(AccountViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult<IActionResult>(BadRequest(ModelState));
            }

            return Task.FromResult<IActionResult>(NotFound());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public override async Task<IActionResult> Logout()
        {
            // Capture id_token_hint BEFORE clearing the cookie - same reasoning as PTL.ExternalWeb's
            // CIDM sign-out: without it, Entra ID ignores post_logout_redirect_uri and shows its own
            // default sign-out page instead of redirecting back to /Account/SignedOut.
            var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var idToken = cookieResult?.Properties?.GetTokenValue("id_token");

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(SignedOut), "Account") };
            if (!string.IsNullOrEmpty(idToken))
            {
                properties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
            }

            return SignOut(properties, EntraAuthenticationDefaults.AuthenticationScheme);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult SignedOut() => View();

        // Reached via EntraOpenIdConnectEvents.TokenValidated's denial redirect when no tblUsers row
        // matches - deliberately anonymous (no session is ever created for a denied sign-in) and
        // rendered with a minimal layout (no header/phase-banner/sign-out link), per the explicit
        // requirement that a denied internal user sees nothing resembling a signed-in page.
        [HttpGet]
        [AllowAnonymous]
        public IActionResult NotPermitted() => View();
    }
}
