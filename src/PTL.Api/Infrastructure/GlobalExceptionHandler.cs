using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace PTL.Api.Infrastructure;

// Catches any exception not already handled by a controller (e.g. CustomerValidationException is
// handled locally in CustomerController and never reaches here). Logs the failure and returns a
// ProblemDetails response instead of letting ASP.NET Core's default unhandled-exception behaviour
// leak stack traces in non-Development environments.
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, string, Exception?> LogUnhandledExceptionMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(1, nameof(LogUnhandledExceptionMessage)),
            "Unhandled exception processing {Method} {Path}");

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnhandledExceptionMessage(logger, httpContext.Request.Method, httpContext.Request.Path.ToString(), exception);

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
