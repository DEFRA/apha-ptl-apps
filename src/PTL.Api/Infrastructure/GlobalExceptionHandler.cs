using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PTL.Api.Infrastructure;

// Catches any exception not already handled by a controller (e.g. CustomerValidationException is
// handled locally in CustomerController and never reaches here). Logs the failure and returns a
// ProblemDetails response instead of letting ASP.NET Core's default unhandled-exception behaviour
// leak stack traces in non-Development environments.
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "The request could not be completed. Please try again."
        }, cancellationToken);

        return true;
    }
}
