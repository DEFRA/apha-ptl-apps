using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Features.Account
{
    public class AccountController : PtlAccountControllerBase<AccountViewModel>
    {
        protected override string PostLoginRedirectController => "Home";

        protected override AccountViewModel CreateLoginModel(string? returnUrl = null) =>
            new() { ReturnUrl = returnUrl };
    }
}
