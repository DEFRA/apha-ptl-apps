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
        public override IActionResult Login(string? returnUrl = null) =>
            ChallengeExternalLogin(EntraAuthenticationDefaults.AuthenticationScheme, returnUrl);

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
        public override Task<IActionResult> Logout() =>
            SignOutViaExternalSchemeAsync(EntraAuthenticationDefaults.AuthenticationScheme);

        [HttpGet]
        [AllowAnonymous]
        public IActionResult SignedOut() => View();

        // Reached via EntraOpenIdConnectEvents.TokenValidated's denial redirect when no tblUsers row
        // matches - deliberately anonymous (no session is ever created for a denied sign-in). Uses the
        // normal site layout (header/phase-banner/footer); no sign-out link appears since the header's
        // sign-out panel is itself conditional on an authenticated session, which never exists here.
        [HttpGet]
        [AllowAnonymous]
        public IActionResult NotPermitted() => View();
    }
}
