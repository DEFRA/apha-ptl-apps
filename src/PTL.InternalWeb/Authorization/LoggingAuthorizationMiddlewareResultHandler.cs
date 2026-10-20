using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using PTL.InternalWeb.Features.Account;

namespace PTL.InternalWeb.Authorization;

// Logs every refused request (not signed in, or signed in without the required role) before
// delegating to the default handler - satisfies the System Administration business rule "a
// non-Admin user's request is refused AND LOGGED" for every [Authorize]-gated controller/action in
// this app (SystemAdministration/Contracts/Scheme policies, plus the SetFallbackPolicy "must be
// signed in" default). Only the opaque InternalUserId claim is logged, never full claims/PII.
public sealed partial class LoggingAuthorizationMiddlewareResultHandler(ILogger<LoggingAuthorizationMiddlewareResultHandler> logger) : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (!authorizeResult.Succeeded)
        {
            var internalUserId = context.User.FindFirst(InternalUserClaimTypes.InternalUserId)?.Value ?? "anonymous";
            LogAuthorizationRefused(internalUserId, context.Request.Path.Value ?? string.Empty);
        }

        await DefaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Access refused: user {InternalUserId} is not permitted to access {Path}")]
    private partial void LogAuthorizationRefused(string internalUserId, string path);
}
