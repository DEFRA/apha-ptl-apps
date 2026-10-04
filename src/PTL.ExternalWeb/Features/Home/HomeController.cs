using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;

namespace PTL.ExternalWeb.Features.Home;

// Privacy is external-facing only; the internal application has no such page.
public class HomeController(IApiClient apiClient) : HomeControllerBase(apiClient)
{
    public IActionResult Privacy() => View();
}
