using Microsoft.AspNetCore.Mvc;
using PTL.InternalWeb.Navigation;

namespace PTL.InternalWeb.Confirmation;

public static class ConfirmationControllerExtensions
{
    private const string AntiForgeryField = "__RequestVerificationToken";

    // Returns the shared confirmation page for the POST being handled. Confirm re-posts every field of
    // the original form (plus confirmation=confirm) to the same URL, so the action runs unchanged on the
    // second request. breadcrumbAction names the page the user came from, as BreadcrumbRouteValues.Action.
    public static ViewResult ConfirmationPage(this Controller controller, ConfirmationPageViewModel model, string? breadcrumbAction = null)
    {
        var request = controller.HttpContext?.Request;

        if (breadcrumbAction is not null)
        {
            controller.ViewData[BreadcrumbRouteValues.Action] = breadcrumbAction;
        }

        return controller.View(ConfirmationFlow.ViewName, model with
        {
            PostUrl = PostedUrl(request),
            Fields = PostedFields(request, ConfirmationFlow.FieldName),
        });
    }

    // The URL the page being answered was posted to, so the follow-up post lands on the same action.
    internal static string PostedUrl(HttpRequest? request) =>
        request is null ? string.Empty : $"{request.PathBase}{request.Path}{request.QueryString}";

    // Every field of the posted form except the antiforgery token and the named ones, for a page that
    // echoes the original form back as hidden inputs.
    internal static List<KeyValuePair<string, string>> PostedFields(HttpRequest? request, params string[] excludedFields) =>
        request is { HasFormContentType: true }
            ? request.Form
                .Where(entry => entry.Key != AntiForgeryField && !excludedFields.Contains(entry.Key, StringComparer.Ordinal))
                .SelectMany(entry => entry.Value.Select(value => new KeyValuePair<string, string>(entry.Key, value ?? string.Empty)))
                .ToList()
            : [];
}
