using System.Security.Claims;
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
        [AllowAnonymous]
        public override IActionResult Login(string? returnUrl = null) =>
            ChallengeExternalLogin(EntraAuthenticationDefaults.AuthenticationScheme, returnUrl);

        // The username/password form this posts to is never rendered for this app (Login(GET) above
        // always challenges Entra ID instead), so this path should be unreachable.
        [HttpPost]
        [AllowAnonymous]
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
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public override Task<IActionResult> Logout() =>
            SignOutViaExternalSchemeAsync(EntraAuthenticationDefaults.AuthenticationScheme);

        [HttpGet]
        [AllowAnonymous]
        public IActionResult SignedOut() => View();

        // Called by session-timeout.js while the "Extend session?" warning is NOT showing, to reset
        // the client's inactivity timer. Requires no body/response beyond success - reaching this
        // [Authorize]-by-default action at all (the request pipeline's EntraCookieEvents.ValidatePrincipal
        // already ran) is itself the proof the session is still genuinely valid; a failed/redirected
        // fetch response tells the caller to show Session Expired immediately instead of resetting.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExtendSession() => NoContent();

        // Reached when the inactivity warning's countdown elapses with no action (or, with
        // JavaScript disabled, via the partial's <noscript> meta-refresh). Signs out of THIS app's
        // own cookie only (not a full Entra federated sign-out/redirect) so the message below
        // reflects reality - the user really is signed out - without navigating away before they've
        // seen it. Always signed out unconditionally (not gated on IsAuthenticated) so a stale/
        // partially-valid principal can never slip through. NoStore so a cached copy of this
        // response (or the browser's back/forward cache) can never mask the sign-out that just
        // happened. "Sign in again" below re-enters the normal Login challenge; a live Entra SSO
        // session reauthenticates automatically, otherwise Entra's own sign-in prompt appears.
        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> SessionExpired()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // SignOutAsync only clears the cookie for FUTURE requests (via Set-Cookie) - it does not
            // retroactively update HttpContext.User for this SAME request, which the authentication
            // middleware already populated before this action ran. Without this, the layout's header
            // partial (which reads User.Identity.IsAuthenticated) would still show "Signed in as" on
            // this very page, even though the user has just been signed out.
            HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            return View();
        }

        // Reached via EntraOpenIdConnectEvents.TokenValidated's denial redirect when no tblUsers row
        // matches - deliberately anonymous (no session is ever created for a denied sign-in). Uses the
        // normal site layout (header/phase-banner/footer); no sign-out link appears since the header's
        // sign-out panel is itself conditional on an authenticated session, which never exists here.
        [HttpGet]
        [AllowAnonymous]
        public IActionResult NotPermitted() => View();

        // Where a refused request lands (cookie AccessDeniedPath). Anonymous so the page itself can never be
        // refused, and it answers 403 rather than 200.
        [HttpGet("/AccessDenied")]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }
    }
}
