using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace PTL.Common.Correlation;

/// <summary>
/// Registers <see cref="CorrelationIdMiddlewareExtensions"/>'s middleware. Shared by PTL.Api,
/// PTL.InternalWeb and PTL.ExternalWeb so a single request/response across services carries the
/// same correlation ID.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>Name of the header carrying the correlation ID, inbound and outbound.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// Reads <see cref="HeaderName"/> from the inbound request (generating a new GUID if absent
    /// or malformed), stores it on <see cref="HttpContext.Items"/> for the lifetime of the
    /// request, echoes it on the response, and pushes it into the Serilog <see cref="LogContext"/>
    /// so every log line for the request carries it. Call this before
    /// <c>UseSerilogRequestLogging()</c>.
    /// </summary>
    /// <param name="app">The application pipeline builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var correlationId = ResolveCorrelationId(context);

            context.Items[HeaderName] = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next();
            }
        });
    }

    // Inbound IDs come from outside the trust boundary (a browser for the web front-ends, or a
    // web front-end for PTL.Api), so only a well-formed GUID is trusted; anything else is
    // replaced rather than logged verbatim.
    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var header) &&
            Guid.TryParse(header.ToString(), out var parsed))
        {
            return parsed.ToString();
        }

        return Guid.NewGuid().ToString();
    }
}
