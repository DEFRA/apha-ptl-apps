using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;
using PTL.ExternalWeb.Models;

namespace PTL.ExternalWeb.Features.Home;

public class HomeController(IApiClient apiClient) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    // Diagnostic endpoint proving Web -> Api connectivity; useful as a smoke-test in any environment.
    public async Task<IActionResult> ApiStatus(CancellationToken cancellationToken)
    {
        var health = await apiClient.GetHealthAsync(cancellationToken);
        return Json(health);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
