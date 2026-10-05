using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PTL.ApiClient;

/// <summary>
/// Shared Home controller actions for every PTL web front-end (PTL.InternalWeb,
/// PTL.ExternalWeb, ...) - identical for each, so it lives here rather than being
/// duplicated per app.
/// </summary>
public abstract class HomeControllerBase(IApiClient apiClient) : Controller
{
    // Requires authentication (via the app's default authorization policy) - the signed-in landing page.
    public IActionResult Index() => View();

    // Diagnostic endpoint proving Web -> Api connectivity; useful as a smoke-test in any environment.
    [AllowAnonymous]
    public async Task<IActionResult> ApiStatus(CancellationToken cancellationToken)
    {
        var health = await apiClient.GetHealthAsync(cancellationToken);
        return Json(health);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
