using System.Text.RegularExpressions;

namespace PTL.Core.Scheme;

// Port of legacy fnCheckPTNumber: the identifier is upper-cased and given a PT prefix when the
// user types digits only. Legacy does this in the browser; doing it server-side too keeps the
// stored value canonical whichever client calls the API.
public static partial class SchemeIdentifier
{
    [GeneratedRegex("^[0-9]{1,4}$", RegexOptions.None, 1000)]
    private static partial Regex DigitsOnly();

    public static string Normalise(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return string.Empty;
        }

        var trimmed = identifier.Trim().ToUpperInvariant();
        return DigitsOnly().IsMatch(trimmed) ? $"PT{trimmed}" : trimmed;
    }
}
