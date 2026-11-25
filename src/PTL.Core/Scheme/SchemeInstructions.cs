using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Ganss.Xss;

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

    // ITHC-001: a regex cannot parse HTML the way a browser does (an unterminated tag, for example, was left in place
    // and completed by the page markup after it), so the allow-list is enforced by a real HTML parser instead. The
    // configured instance is never changed after construction, which is what makes it safe to share across threads.
    private static readonly HtmlSanitizer Sanitiser = CreateSanitiser();

    // Safe span styles noted during a sanitise, keyed by the element (each call parses its own DOM, and the table is
    // thread-safe and holds its keys weakly, so concurrent calls never see each other's entries).
    private static readonly ConditionalWeakTable<IElement, string> SafeSpanStyles = new();

    private const int MaxPasses = 5;

    [GeneratedRegex("</?([a-zA-Z][a-zA-Z0-9]*)\\b[^>]*>", RegexOptions.None, 1000)]
    private static partial Regex TagPattern();

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

        // Each pass's decode step can turn text that a previous pass escaped back into markup, so the result is only
        // final once a pass leaves it unchanged - which makes sanitising twice the same as sanitising once. Every pass
        // ends in the sanitiser, so whatever is returned has been through it. Editor output settles on the first pass.
        var current = html;
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var next = Sanitiser.Sanitize(CollapseAccidentalDoubleEncoding(current));
            if (next == current)
            {
                break;
            }

            current = next;
        }

        return current;
    }

    // Nothing but the allow-listed tags survives: no attributes, URLs, classes, CSS or comments. Disallowed elements
    // (including the hyperlinks the editor inserted for emails and URLs) are removed but their text is kept.
    private static HtmlSanitizer CreateSanitiser()
    {
        var sanitiser = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(AllowedTags, StringComparer.OrdinalIgnoreCase),
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            AllowedAtRules = new HashSet<CssRuleType>(),
        })
        {
            KeepChildNodes = true,
        };

        // The one attribute exception: a span may keep a style that is purely an underline or strikethrough. Style is
        // not an allowed attribute, so it is removed like any other; a safe one is noted as it goes and put back once
        // sanitising has finished, exactly as written (re-serialising it through the CSS parser would change the
        // stored text). The anchored pattern admits nothing else, so the value restored is inert.
        sanitiser.RemovingAttribute += (_, e) =>
        {
            if (e.Reason == RemoveReason.NotAllowedAttribute
                && e.Tag.LocalName == "span"
                && e.Attribute.LocalName == "style"
                && SafeStylePattern().IsMatch(e.Attribute.Value))
            {
                SafeSpanStyles.AddOrUpdate(e.Tag, e.Attribute.Value.Trim());
            }
        };
        sanitiser.PostProcessNode += (_, e) =>
        {
            if (e.Node is IElement element && SafeSpanStyles.TryGetValue(element, out var style))
            {
                element.SetAttribute("style", style);
                SafeSpanStyles.Remove(element);
            }
        };

        return sanitiser;
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
