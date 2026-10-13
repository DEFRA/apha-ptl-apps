using System.Text.RegularExpressions;

namespace PTL.Core.ExternalSiteMessage;

// Further Information (Message) has no validation at all, matching legacy (TxtMessage on
// EditMessageOnWebsite.aspx carries no validators despite the varchar(8000) column).
public static partial class ExternalSiteMessageValidator
{
    private const int ImportantMessageMaxVisibleCharacters = 500;

    // Matches legacy's shared GlobalResources.EmailRegEx exactly (used by both
    // MainPageMessage.AddBusinessRules and EditMessageOnWebsite.aspx's ValidatorEmailRegEx) - not
    // the different, stricter regex hardcoded on ManageExternalTCs.aspx/ManageViewers.aspx.
    [GeneratedRegex(@"^[a-zA-Z0-9'._%-]+@[a-zA-Z0-9.-]+\.[a-zA-Z0-9-]{2,}$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTagPattern();

    public static ExternalSiteMessageValidationResult Validate(string importantMessage, string supportEmailAddress)
    {
        var errors = new List<ExternalSiteMessageValidationError>();

        // Counts VISIBLE characters only (tags stripped first), matching legacy's
        // StripStyling.CountVisibleCharacters - counting raw HTML length would reject valid
        // content purely because of bold/italic/underline/link markup overhead.
        if (CountVisibleCharacters(importantMessage) > ImportantMessageMaxVisibleCharacters)
        {
            errors.Add(new ExternalSiteMessageValidationError(
                nameof(ExternalSiteMessage.ImportantMessage),
                $"The Important Message cannot exceed {ImportantMessageMaxVisibleCharacters} characters."));
        }

        if (string.IsNullOrWhiteSpace(supportEmailAddress))
        {
            errors.Add(new ExternalSiteMessageValidationError(nameof(ExternalSiteMessage.SupportEmailAddress), "The Email Address is required"));
        }
        else if (!EmailPattern().IsMatch(supportEmailAddress))
        {
            errors.Add(new ExternalSiteMessageValidationError(nameof(ExternalSiteMessage.SupportEmailAddress), "The Email Address is invalid"));
        }

        return new ExternalSiteMessageValidationResult(errors.Count == 0, errors);
    }

    // Strip tags -> decode entities -> collapse &nbsp; -> trim. Deliberately dependency-free
    // (no HtmlAgilityPack) rather than exactly reproducing legacy's implementation - close enough
    // for well-formed TinyMCE-generated markup. Public so it's independently unit-testable.
    public static int CountVisibleCharacters(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return 0;
        }

        var decoded = System.Net.WebUtility.HtmlDecode(html);
        var textOnly = HtmlTagPattern().Replace(decoded, string.Empty);
        textOnly = System.Net.WebUtility.HtmlDecode(textOnly).Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);
        return textOnly.Trim().Length;
    }
}

public sealed record ExternalSiteMessageValidationError(string Field, string Message);
public sealed record ExternalSiteMessageValidationResult(bool IsValid, IReadOnlyList<ExternalSiteMessageValidationError> Errors);
