using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using PTL.ExternalWeb.Features.Account;

namespace PTL.ExternalWeb.Authorization;

// Logs every refused request (not signed in, or signed in without the required role) before
// delegating to the default handler - mirrors
// PTL.InternalWeb.Authorization.LoggingAuthorizationMiddlewareResultHandler, bringing ExternalWeb's
// audit trail to parity with InternalWeb's. Only the opaque ParticipantId claim is logged when
// present (Viewer/Test Consultant-only users have no numeric id claim today), never names/emails.
public sealed partial class ExternalAccessDeniedLoggingHandler(ILogger<ExternalAccessDeniedLoggingHandler> logger) : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (!authorizeResult.Succeeded)
        {
            var participantId = context.User.FindFirst(ExternalUserClaimTypes.ParticipantId)?.Value ?? "unknown";
            LogAuthorizationRefused(participantId, context.Request.Path.Value ?? string.Empty);
        }

        await DefaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Access refused: participant {ParticipantId} is not permitted to access {Path}")]
    private partial void LogAuthorizationRefused(string participantId, string path);
}
