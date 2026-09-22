using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace PTL.ApiClient;

/// <summary>
/// Shared cookie sign-in/sign-out mechanics for the PTL web front-ends' Account controllers -
/// identical for each app except where a successful login redirects to, which the derived
/// controller supplies via <see cref="PostLoginRedirectController"/>.
/// </summary>
public abstract class PtlAccountControllerBase<T> : Controller where T : class, IAccountCredentials
{
    protected abstract string PostLoginRedirectController { get; }

    protected abstract T CreateLoginModel(string? returnUrl = null);

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        var model = CreateLoginModel(returnUrl);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(T model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(string.Empty, "Please provide username and password.");
            return View(model ?? CreateLoginModel());
        }

        return await SignInAndRedirectAsync(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Logout() => SignOutAndRedirectToHomeAsync();

    protected async Task<IActionResult> SignInAndRedirectAsync(IAccountCredentials model)
    {
        var claims = new[] { new Claim(ClaimTypes.Name, model.Username!) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var authProperties = new AuthenticationProperties { IsPersistent = false };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", PostLoginRedirectController);
    }

    protected async Task<IActionResult> SignOutAndRedirectToHomeAsync()
    {
        TempData["LoginMessage"] = "Signed out";
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
}

public static class ModelStateExtensions
{
    public static void AddFieldErrors(this ControllerBase controller, IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        foreach (var (field, messages) in fieldErrors)
        {
            foreach (var message in messages)
            {
                controller.ModelState.AddModelError(field, message);
            }
        }
    }
}
