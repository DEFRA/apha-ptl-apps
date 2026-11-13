namespace PTL.ExternalWeb.Features.Order;

// Settings for the order wizard pages, bound from the "CreateOrder" configuration section.
public sealed class OrderPageOptions
{
    public const string SectionName = "CreateOrder";

    // Legacy read this from the app setting TermsAndConditionsWebLink. The PDF ships with this app (wwwroot/documents), so the default is a
    // site-relative path (spaces and the ampersand encoded). Blank, the Terms and Conditions link is not shown.
    public string? TermsAndConditionsUrl { get; set; } = "/documents/VETQAS%20Ts%20%26%20Cs%20April%202025%20Version%206%201.pdf";

    // Legacy read this from the app setting DistributionTimetableWebLink. Left blank, "website" is shown as plain text.
    public string? DistributionTimetableUrl { get; set; } = "https://www.aphascientific.com/services/vetqas-proficiency-testing/";

    // Only an absolute http(s) address or a path on this site is rendered as a link, so a mistyped or hostile value can never become a script URL.
    public string? SafeTermsAndConditionsUrl => SafeSitePath(TermsAndConditionsUrl) ?? SafeHttpUrl(TermsAndConditionsUrl);

    public string? SafeDistributionTimetableUrl => SafeHttpUrl(DistributionTimetableUrl);

    // A single leading slash only: "//host" and "/\host" would send the user to another site.
    private static string? SafeSitePath(string? value)
    {
        var path = value?.Trim();
        return path is { Length: > 1 } && path[0] == '/' && path[1] != '/' && path[1] != '\\' ? path : null;
    }

    private static string? SafeHttpUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.AbsoluteUri
            : null;
}
