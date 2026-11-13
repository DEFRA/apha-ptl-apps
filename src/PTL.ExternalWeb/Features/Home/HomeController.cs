using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.Common.Auth;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.ViewSupport;

namespace PTL.ExternalWeb.Features.Home;

// Privacy is external-facing only; the internal application has no such page.
public class HomeController(IApiClient apiClient, ISystemMessageApiClient systemMessageApiClient, ICreateOrderApiClient createOrderApiClient) : HomeControllerBase(apiClient)
{
    // Overrides the shared HomeControllerBase.Index() (used as-is by PTL.InternalWeb) to show the
    // CIDM display name and resolved roles, plus the admin-authored "Important Message" banner
    // (tblExtWebsiteMessage - see legacy ExternalWeb Home.aspx) - external-sign-in-specific, so it
    // doesn't belong in the shared base used by both front-ends.
    public override async Task<IActionResult> Index()
    {
        var displayName = User.GetDisplayName(preferredClaimType: ExternalUserClaimTypes.DisplayName) ?? string.Empty;
        var importantMessage = await systemMessageApiClient.GetImportantMessageAsync(HttpContext.RequestAborted);
        var eligibility = await OrderEligibilityLookup.GetAsync(User, createOrderApiClient, HttpContext.RequestAborted);
        return View(new HomeIndexViewModel(
            displayName,
            User.GetResolvedExternalRoles(),
            DecodeStoredHtml(importantMessage.ImportantMessage),
            eligibility.CanCreateCurrentYearOrder,
            eligibility.CanCreateNextYearOrder,
            eligibility.CurrentYear,
            eligibility.NextYear));
    }

    [AllowAnonymous]
    public IActionResult Cookies() => View(new CookiesViewModel { UseAnalyticsCookies = IsAnalyticsCookiesEnabled() });

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cookies(CookiesViewModel model, string? returnUrl = null)
    {
        SaveCookiePreferences(model.UseAnalyticsCookies);
        TempData["CookiePreferencesUpdated"] = model.UseAnalyticsCookies ? "accepted" : "rejected";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        var referer = Request.Headers.Referer.ToString();
        if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
        {
            var localRefererPath = $"{refererUri.AbsolutePath}{refererUri.Query}";
            if (Url.IsLocalUrl(localRefererPath))
            {
                return Redirect(localRefererPath);
            }
        }

        return RedirectToAction(nameof(Cookies));
    }

    // Legal/informational page - deliberately anonymous, like PTL.ExternalWeb's other
    // [AllowAnonymous] pages (ApiStatus, Error), since it must be reachable before sign-in.
    [AllowAnonymous]
    public IActionResult Privacy() => View();

    // Legacy AccessibilityStatement.aspx (footer link) - anonymous like Privacy, so it is reachable before sign-in.
    [AllowAnonymous]
    public IActionResult Accessibility() => View();

    // Legacy ViewInformation.aspx, linked from Index's static "Important" banner - shows
    // tblExtWebsiteMessage.fldMessage, requires sign-in (same as legacy).
    public async Task<IActionResult> Information()
    {
        var message = await systemMessageApiClient.GetMessageAsync(HttpContext.RequestAborted);
        return View(new InformationViewModel(DecodeStoredHtml(message.Message)));
    }

    // Rows saved before the save-time decode fix hold entity-escaped HTML; decoding here repairs them without a data patch.
    private static string? DecodeStoredHtml(string? stored) => stored is null ? null : WebUtility.HtmlDecode(stored);

    private bool IsAnalyticsCookiesEnabled() =>
        string.Equals(Request.Cookies[CookiePreferenceNames.Analytics], bool.TrueString, StringComparison.OrdinalIgnoreCase);

    private void SaveCookiePreferences(bool useAnalyticsCookies)
    {
        var cookieOptions = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddMonths(6),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps
        };

        Response.Cookies.Append(CookiePreferenceNames.ConsentSet, bool.TrueString, cookieOptions);
        Response.Cookies.Append(CookiePreferenceNames.Analytics, useAnalyticsCookies ? bool.TrueString : bool.FalseString, cookieOptions);
    }
}

public sealed class CookiesViewModel
{
    public bool UseAnalyticsCookies { get; set; }
}
