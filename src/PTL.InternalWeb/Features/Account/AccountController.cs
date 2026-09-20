using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.InternalWeb.Features.Account;

namespace PTL.InternalWeb.Features.Account
{
    public class AccountController : PtlAccountControllerBase
    {
        protected override string PostLoginRedirectController => "Customer";

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            var model = new AccountViewModel { ReturnUrl = returnUrl };
            // Let the view engine locate the view using registered locations (Features/...)
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(AccountViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError(string.Empty, "Please provide username and password.");
                return View(model ?? new AccountViewModel());
            }

            return await SignInAndRedirectAsync(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Logout() => SignOutAndRedirectToHomeAsync();
    }
}
