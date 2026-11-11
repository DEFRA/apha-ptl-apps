namespace PTL.ExternalWeb.Features.Order;

// Settings for the order wizard pages, bound from the "CreateOrder" configuration section.
public sealed class OrderPageOptions
{
    public const string SectionName = "CreateOrder";

    // Legacy read this from the app setting TermsAndConditionsWebLink. Left blank, the Terms and Conditions link is not shown.
    public string? TermsAndConditionsUrl { get; set; }

    // Legacy read this from the app setting DistributionTimetableWebLink. Left blank, "website" is shown as plain text.
    public string? DistributionTimetableUrl { get; set; }

    // Only an absolute http(s) address is rendered as a link, so a mistyped or hostile value can never become a script URL.
    public string? SafeTermsAndConditionsUrl => SafeHttpUrl(TermsAndConditionsUrl);

    public string? SafeDistributionTimetableUrl => SafeHttpUrl(DistributionTimetableUrl);

    private static string? SafeHttpUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.AbsoluteUri
            : null;
}
