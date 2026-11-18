using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;

namespace PTL.InternalWeb.Features.Home;

public class HomeController(IApiClient apiClient) : HomeControllerBase(apiClient)
{
    // Footer pages are anonymous so they stay reachable before sign-in.
    [AllowAnonymous]
    [HttpGet("/Accessibility")]
    public IActionResult Accessibility() => View();

    [AllowAnonymous]
    [HttpGet("/Cookies")]
    public IActionResult Cookies() => View();
}
