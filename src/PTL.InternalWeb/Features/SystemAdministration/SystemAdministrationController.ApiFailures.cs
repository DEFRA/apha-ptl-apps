using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Polly;
using PTL.SharedUI.Notifications;

namespace PTL.InternalWeb.Features.SystemAdministration;

// When PTL.Api cannot be reached (after the HttpClient retry policy has given up), a System Administration page
// shows an error banner on a page that does not call the API, rather than the generic error page.
public partial class SystemAdministrationController
{
    private static readonly Action<ILogger, string, Exception?> LogApiUnavailableMessage =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(102, nameof(LogApiUnavailableMessage)),
            "PTL.Api could not be reached for {Action}");

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Exception is { } exception
            && !context.ExceptionHandled
            && IsApiUnavailable(exception, context.HttpContext.RequestAborted))
        {
            LogApiUnavailableMessage(logger, context.ActionDescriptor.DisplayName ?? "a System Administration action", exception);
            TempData.SetNotification(NotificationType.Error, "This could not be completed because a service is not responding. Please try again later.");
            context.ExceptionHandled = true;
            context.Result = RedirectAfterApiFailure(context.HttpContext.Request);
        }

        base.OnActionExecuted(context);
    }

    private static bool IsApiUnavailable(Exception exception, CancellationToken requestAborted) =>
        exception is HttpRequestException or ExecutionRejectedException
        || (exception is OperationCanceledException && !requestAborted.IsCancellationRequested);

    // A page that failed to load goes back to the landing page (it makes no API calls, so it cannot loop); a
    // failed save goes back to the page it was posted from.
    private IActionResult RedirectAfterApiFailure(HttpRequest request)
    {
        if (HttpMethods.IsPost(request.Method)
            && Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
            && Url.IsLocalUrl(referer.PathAndQuery))
        {
            return LocalRedirect(referer.PathAndQuery);
        }

        return RedirectToAction(nameof(Index));
    }
}
