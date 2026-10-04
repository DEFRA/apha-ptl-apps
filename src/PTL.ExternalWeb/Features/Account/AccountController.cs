using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Auth.Cidm;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Features.Account
{
    // Sign-in for this app is entirely via CIDM (Gov.UK One Login / Government Gateway / RPA / Trusted
    // Third Party, brokered by DEFRA Customer Identity) - the base class's username/password form is a
    // stub shared with PTL.InternalWeb only, and is not used here.
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

            return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, CidmAuthenticationDefaults.AuthenticationScheme);
        }

        // The username/password form this posts to is never rendered for this app (Login(GET) above
        // always challenges CIDM instead), so this path should be unreachable - disabled rather than
        // left wired to the base class's fake-identity sign-in. The base method's
        // [ValidateAntiForgeryToken] still applies here (MVC's filter discovery picks it up by
        // reflection even on an override that doesn't redeclare it), so a token-less POST is
        // rejected before reaching this body - either outcome means no sign-in ever happens.
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
            // Capture id_token_hint BEFORE clearing the cookie. OpenIdConnectHandler.HandleSignOutAsync
            // only reads id_token_hint from the AuthenticationProperties passed to SignOut() (empty
            // here) or, failing that, falls back to re-authenticating against the cookie scheme - which
            // finds nothing once the cookie below has already been cleared. Without id_token_hint, CIDM
            // (Azure AD B2C) ignores post_logout_redirect_uri entirely and shows its own default
            // sign-out page instead of redirecting back to /Account/SignedOut - this was the root cause
            // of sign-out landing on CIDM's page instead of ours.
            var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var idToken = cookieResult?.Properties?.GetTokenValue("id_token");

            // Signed out separately (not via a single SignOut(cookie, oidc) call sharing one
            // AuthenticationProperties): CookieAuthenticationHandler.SignOutAsync also honours
            // RedirectUri and issues its own redirect immediately, which would hijack the response
            // before the OIDC scheme's sign-out event ever got a chance to render CIDM's
            // end_session_endpoint form. The cookie is cleared with no redirect of its own; only the
            // CIDM sign-out carries the RedirectUri, used once the round trip back from CIDM completes.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(SignedOut), "Account") };
            if (!string.IsNullOrEmpty(idToken))
            {
                properties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
            }

            return SignOut(properties, CidmAuthenticationDefaults.AuthenticationScheme);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult SignedOut() => View();
    }
}
