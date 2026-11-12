using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Auth.Cidm;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.Features.Lab;

namespace PTL.ExternalWeb.Features.Account
{
    // Sign-in for this app is entirely via CIDM (Gov.UK One Login / Government Gateway / RPA / Trusted
    // Third Party, brokered by DEFRA Customer Identity) - the base class's username/password form is a
    // stub shared with PTL.InternalWeb only, and is not used here.
    // Sign-out must work before a lab has been chosen.
    [SkipLabSelection]
    public class AccountController : PtlAccountControllerBase<AccountViewModel>
    {
        protected override string PostLoginRedirectController => "Home";

        protected override AccountViewModel CreateLoginModel(string? returnUrl = null) =>
            new() { ReturnUrl = returnUrl };

        [HttpGet]
        public override IActionResult Login(string? returnUrl = null) =>
            ChallengeExternalLogin(CidmAuthenticationDefaults.AuthenticationScheme, returnUrl);

        // The username/password form this posts to is never rendered for this app (Login(GET) above
        // always challenges CIDM instead), so this path should be unreachable - disabled rather than
        // left wired to the base class's fake-identity sign-in. The base method's
        // [ValidateAntiForgeryToken] still applies here (MVC's filter discovery picks it up by
        // reflection even on an override that doesn't redeclare it), so a token-less POST is
        // rejected before reaching this body - either outcome means no sign-in ever happens.
        [HttpPost]
        [ValidateAntiForgeryToken]
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
        public override Task<IActionResult> Logout() =>
            SignOutViaExternalSchemeAsync(CidmAuthenticationDefaults.AuthenticationScheme);

        [HttpGet]
        [AllowAnonymous]
        public IActionResult SignedOut() => View();

        // Called by session-timeout.js when the user selects Yes on the "Extend Session?" warning. Reaching
        // this [Authorize]-by-default action at all (cookie validation and CIDM token refresh already ran in
        // the pipeline) proves the session is still valid and slides the cookie; a failed or redirected
        // response tells the script to show Session Expired instead.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExtendSession() => NoContent();

        // Reached when the warning's countdown elapses with no action. Signs out of THIS app's cookie only -
        // not CIDM's federated sign-out - so a live CIDM single sign-on session re-authorises the user
        // automatically when they select OK, and CIDM's own sign-in appears otherwise. Signed out
        // unconditionally so a stale principal can never slip through; NoStore so a cached copy can never
        // mask the sign-out that just happened.
        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> SessionExpired()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // SignOutAsync only affects later requests; clear the user for this response so the header
            // does not still show "Signed in as" on the page that says the session has ended.
            HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            return View();
        }

        // Reached via ExternalUserResolver's denial redirect when the CIDM identity doesn't resolve to
        // an active PTL record - no local cookie exists at this point, since TokenValidated redirects
        // here before the OIDC handler ever signs the principal into the cookie scheme. reason is
        // one of NotPermittedReasons (inactive record, or a Lab User with no active lab) so the view can
        // show more specific guidance than the generic case. Any other value shows the generic message.
        [HttpGet]
        [AllowAnonymous]
        public IActionResult NotPermitted(string? reason) => View(model: reason);

        // Where a refused request lands (cookie AccessDeniedPath) - reached when a signed-in user
        // lacks the role required for the page/link they followed, via any
        // [Authorize(Policy=...)]-gated controller. Anonymous so the page itself can never be
        // refused, and it answers 403 rather than 200 - mirrors PTL.InternalWeb's equivalent.
        [HttpGet("/AccessDenied")]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }
    }
}
