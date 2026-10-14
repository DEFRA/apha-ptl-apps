using System.Text.RegularExpressions;

namespace PTL.Core.Distribution;

// Ports legacy SampleNumbers/IntendedResults.aspx.vb's RegularExpressionValidator wiring for
// Intended Result values. A FormatString starting with "^" means the Scheme configured a regex
// return type (rendered as a free-text box); anything else is a "|"-delimited list of options
// (rendered as a dropdown). Legacy has NO RequiredFieldValidator anywhere on this screen (the
// commented-out code in IntendedResults.aspx.vb proves it was deliberately removed) and NO
// validator at all constraining a dropdown value to one of its own options, so this validator
// must not invent either rule - only the regex format check is real, and only for regex items,
// and only when a value is actually supplied (ASP.NET's RegularExpressionValidator never fires on
// an empty value either).
public static class IntendedResultsValidator
{
    public static bool IsRegexFormat(string? formatString) =>
        !string.IsNullOrEmpty(formatString) && formatString.StartsWith('^');

    public static string? ValidateValue(string? formatString, string? returnValueType, string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsRegexFormat(formatString))
        {
            return null;
        }

        return Regex.IsMatch(value, formatString!, RegexOptions.None, TimeSpan.FromMilliseconds(100)) ? null : $"Input must be of format: {returnValueType}";
    }
}
