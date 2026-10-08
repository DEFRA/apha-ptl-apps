using System.Net;
using System.Text.RegularExpressions;

namespace PTL.Core.Scheme;

/// <summary>
/// Server-side handling for the Scheme Instructions rich text. The legacy editor's Text setter
/// stripped the hyperlinks it auto-inserted for emails and URLs and was meant to restrict the
/// markup to a small allow-list; its ValidationProperty("TextPlain") stripped the markup entirely
/// so the RequiredFieldValidator ran against the visible text rather than the HTML.
/// </summary>
public static partial class SchemeInstructions
{
    // Every tag present in live fldInstructions data, plus the ones this toolbar can produce.
    // span is here because TinyMCE renders underline as <span style="text-decoration: underline;">
    // and over a thousand existing rows rely on it.
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "b", "strong", "i", "em", "u", "sup", "sub", "span"
    };

    [GeneratedRegex("<a\\b[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline, 1000)]
    private static partial Regex AnchorPattern();

    [GeneratedRegex("</?([a-zA-Z][a-zA-Z0-9]*)\\b[^>]*>", RegexOptions.None, 1000)]
    private static partial Regex TagPattern();

    [GeneratedRegex("style\\s*=\\s*(\"([^\"]*)\"|'([^']*)')", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex StyleAttributePattern();

    // Underline and strikethrough only - anything else a style could carry is dropped.
    [GeneratedRegex("^\\s*(text-decoration\\s*:\\s*(underline|line-through)\\s*;?\\s*)+$", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex SafeStylePattern();

    // Collapses a run of identical leading/trailing <p> wrappers left by CollapseAccidentalDoubleEncoding.
    [GeneratedRegex("^(?:<p>\\s*)+", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex LeadingParagraphWrapperPattern();

    [GeneratedRegex("(?:\\s*</p>)+$", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex TrailingParagraphWrapperPattern();

    /// <summary>
    /// Drops every tag outside the allow-list and every attribute except a text-decoration style
    /// on a span. Legacy intended to throw "Potentially Dangerous Input" here, but its guard regex
    /// was unanchored and matched the empty string, so it never fired and disallowed markup was
    /// stored verbatim. Stripping is the behaviour legacy meant to have, without an exception no
    /// user ever saw.
    /// </summary>
    public static string Sanitise(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var withoutAnchors = AnchorPattern().Replace(CollapseAccidentalDoubleEncoding(html), "$1");

        return TagPattern().Replace(withoutAnchors, match =>
        {
            var name = match.Groups[1].Value.ToLowerInvariant();
            if (!AllowedTags.Contains(name))
            {
                return string.Empty;
            }

            if (match.Value.StartsWith("</", StringComparison.Ordinal))
            {
                return $"</{name}>";
            }

            return name == "span" && SafeStyle(match.Value) is { } style
                ? $"<span style=\"{style}\">"
                : $"<{name}>";
        });
    }

    // A round trip through the editor can occasionally leave the previous save's tag delimiters -
    // and anything HTML-escaped alongside them, such as &nbsp; - wrapped as plain text inside a
    // new real <p>, so the stored markup gains one more layer of escaping each time (&lt;p&gt;
    // becomes &amp;lt;p&amp;gt;, and so on). Decoding one layer at a time and stopping as soon as
    // that no longer reveals further allow-listed tags collapses the markup back to the single
    // real layer, in step with however many extra layers actually exist, without ever touching a
    // correctly single-encoded entity that was never re-wrapped.
    private static string CollapseAccidentalDoubleEncoding(string html)
    {
        var current = html;
        var tagCount = TagPattern().Count(current);

        for (var i = 0; i < 10; i++)
        {
            var decoded = WebUtility.HtmlDecode(current);
            var decodedTagCount = TagPattern().Count(decoded);
            if (decoded == current || decodedTagCount <= tagCount)
            {
                break;
            }

            current = decoded;
            tagCount = decodedTagCount;
        }

        // Each accumulated layer wrapped the whole of the previous one in its own new <p>, so
        // decoding leaves that many redundant nested wrappers around the original content -
        // collapse a run of identical leading/trailing wrappers down to the single real pair.
        current = LeadingParagraphWrapperPattern().Replace(current, "<p>");
        current = TrailingParagraphWrapperPattern().Replace(current, "</p>");

        return current;
    }

    private static string? SafeStyle(string tag)
    {
        var style = StyleAttributePattern().Match(tag);
        if (!style.Success)
        {
            return null;
        }

        var value = style.Groups[2].Success ? style.Groups[2].Value : style.Groups[3].Value;
        return SafeStylePattern().IsMatch(value) ? value.Trim() : null;
    }

    /// <summary>Legacy TextPlain - what the required-field check actually measured.</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        return TagPattern().Replace(html, string.Empty)
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    /// <summary>False for markup that renders as nothing, such as an empty paragraph.</summary>
    public static bool HasContent(string? html) => ToPlainText(html).Length > 0;
}
