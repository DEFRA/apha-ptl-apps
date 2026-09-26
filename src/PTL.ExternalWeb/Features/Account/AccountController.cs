using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
        public override Task<IActionResult> Login(AccountViewModel model) => Task.FromResult<IActionResult>(NotFound());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public override async Task<IActionResult> Logout()
        {
            // Signed out separately (not via a single SignOut(cookie, oidc) call sharing one
            // AuthenticationProperties): CookieAuthenticationHandler.SignOutAsync also honours
            // RedirectUri and issues its own redirect immediately, which would hijack the response
            // before the OIDC scheme's sign-out event ever got a chance to render CIDM's
            // end_session_endpoint form. The cookie is cleared with no redirect of its own; only the
            // CIDM sign-out carries the RedirectUri, used once the round trip back from CIDM completes.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(SignedOut), "Account") };
            return SignOut(properties, CidmAuthenticationDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public IActionResult SignedOut() => View();
    }
}
