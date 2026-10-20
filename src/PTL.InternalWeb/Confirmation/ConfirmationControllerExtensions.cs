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

        var fields = request is { HasFormContentType: true }
            ? request.Form
                .Where(entry => entry.Key is not AntiForgeryField and not ConfirmationFlow.FieldName)
                .SelectMany(entry => entry.Value.Select(value => new KeyValuePair<string, string>(entry.Key, value ?? string.Empty)))
                .ToList()
            : [];

        if (breadcrumbAction is not null)
        {
            controller.ViewData[BreadcrumbRouteValues.Action] = breadcrumbAction;
        }

        return controller.View(ConfirmationFlow.ViewName, model with
        {
            PostUrl = request is null ? string.Empty : $"{request.PathBase}{request.Path}{request.QueryString}",
            Fields = fields,
        });
    }
}
