using System.Net;
using System.Text.RegularExpressions;

namespace PTL.InternalWeb.Tests.TestSupport;

internal static class BreadcrumbHtml
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    // Ordered crumbs of a rendered page's govuk-breadcrumbs trail; Href is null for a plain-text crumb.
    public static IReadOnlyList<(string Text, string? Href)> Crumbs(string html)
    {
        var nav = Regex.Match(html, "<nav class=\"govuk-breadcrumbs\".*?</nav>", RegexOptions.Singleline, RegexTimeout).Value;
        Assert.True(nav.Length > 0, "No breadcrumbs rendered.");

        return [.. Regex.Matches(nav, "<li class=\"govuk-breadcrumbs__list-item\">(.*?)</li>", RegexOptions.Singleline, RegexTimeout)
            .Select(m => m.Groups[1].Value)
            .Select(inner => (
                Text: WebUtility.HtmlDecode(Regex.Replace(inner, "<[^>]+>", string.Empty, RegexOptions.None, RegexTimeout)).Trim(),
                Href: Regex.Match(inner, "<a\\b[^>]*\\bhref=\"([^\"]+)\"", RegexOptions.None, RegexTimeout) is { Success: true } link
                    ? WebUtility.HtmlDecode(link.Groups[1].Value)
                    : null))];
    }
}
