using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;

namespace PTL.InternalWeb.Features.Home;

// Conventionally routed like the inherited Index/Error/ApiStatus actions, so no attribute route templates here.
public class HomeController(IApiClient apiClient) : HomeControllerBase(apiClient)
{
    // Footer pages are anonymous so they stay reachable before sign-in.
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Accessibility() => View();

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Cookies() => View();
}
