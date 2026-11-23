using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PTL.ApiClient;

namespace PTL.ExternalWeb.Features.ErrorPages;

// Re-executed (Program.cs) for an error response that has no body - mirrors PTL.InternalWeb's ErrorController.
// Re-execution keeps the original method, so this answers a refused POST as well; this action changes nothing.
[AllowAnonymous]
[Route("Error")]
public sealed class ErrorController : Controller
{
    internal const string AccessDeniedView = "/Features/Account/Views/AccessDenied.cshtml";

    [Route("{statusCode:int:range(400,599)}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index(int statusCode)
    {
        Response.StatusCode = statusCode;
        return statusCode switch
        {
            StatusCodes.Status404NotFound => View("PageNotFound"),
            // A bare 403 (e.g. an order the participant may not place) gets the same page as a refused role.
            StatusCodes.Status403Forbidden => View(AccessDeniedView),
            _ => View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier }),
        };
    }
}
