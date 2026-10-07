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
        public override IActionResult Login(string? returnUrl = null) =>
            ChallengeExternalLogin(CidmAuthenticationDefaults.AuthenticationScheme, returnUrl);

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
        public override Task<IActionResult> Logout() =>
            SignOutViaExternalSchemeAsync(CidmAuthenticationDefaults.AuthenticationScheme);

        [HttpGet]
        [AllowAnonymous]
        public IActionResult SignedOut() => View();

        // Reached via ExternalUserResolver's denial redirect when no CIDM role resolves to an
        // existing record - no local cookie exists at this point, since TokenValidated redirects
        // here before the OIDC handler ever signs the principal into the cookie scheme.
        [HttpGet]
        [AllowAnonymous]
        public IActionResult NotPermitted() => View();
    }
}
