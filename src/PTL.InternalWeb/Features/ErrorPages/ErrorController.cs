using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;

namespace PTL.InternalWeb.Features.ErrorPages;

// Re-executed (Program.cs) for an error response that has no body. Re-execution keeps the original method, so this
// answers a refused POST as well; the app has no global antiforgery filter, and this action changes nothing.
[AllowAnonymous]
[Route("Error")]
public sealed class ErrorController : Controller
{
    [Route("{statusCode:int:range(400,599)}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index(int statusCode)
    {
        Response.StatusCode = statusCode;
        if (statusCode == StatusCodes.Status404NotFound)
        {
            return View("PageNotFound");
        }

        return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
