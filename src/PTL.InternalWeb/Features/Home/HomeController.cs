using System.Diagnostics;
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

    // Re-executed (Program.cs) for an error response that has no body. Re-execution keeps the original
    // method, so it must answer a refused POST as well, without asking for its antiforgery token again.
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [Route("/Error/{statusCode:int:range(400,599)}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCodePage(int statusCode)
    {
        Response.StatusCode = statusCode;
        return statusCode == StatusCodes.Status404NotFound
            ? View("PageNotFound")
            : View(nameof(Error), new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
