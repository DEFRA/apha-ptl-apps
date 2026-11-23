using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Polly;
using PTL.ApiClient;

namespace PTL.ExternalWeb.Features.ErrorPages;

/// <summary>The sentence telling an external user who to contact, with the support email an Administrator maintains.</summary>
/// <param name="LeadIn">The start of the sentence, e.g. "If the problem continues, contact the Animal and Plant Health Agency".</param>
/// <param name="EmailAddress">The configured support email address, or null when none is set or it could not be read.</param>
public sealed record SupportContactViewModel(string LeadIn, string? EmailAddress);

// Legacy ErrorGeneric.aspx/ErrorPageNotFound.aspx appended tblExtWebsiteMessage.fldSupportEmailAddress (set on the
// internal External Site Management page). A view component so every error page, including the shared /Home/Error,
// shows it without each controller fetching it.
public sealed partial class SupportContactViewComponent(
    IExternalSiteMessageApiClient externalSiteMessageApiClient,
    ILogger<SupportContactViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string leadIn) =>
        View(new SupportContactViewModel(leadIn, await GetSupportEmailAddressAsync()));

    // An error page is often shown because PTL.Api is unreachable, so a failed lookup must not break the page.
    private async Task<string?> GetSupportEmailAddressAsync()
    {
        try
        {
            var message = await externalSiteMessageApiClient.GetExternalSiteMessageAsync(HttpContext.RequestAborted);
            return string.IsNullOrWhiteSpace(message.SupportEmailAddress) ? null : message.SupportEmailAddress.Trim();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ExecutionRejectedException or JsonException)
        {
            LogSupportEmailUnavailable(logger, exception);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the support email address for an error page.")]
    private static partial void LogSupportEmailUnavailable(ILogger logger, Exception exception);
}
