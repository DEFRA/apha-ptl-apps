namespace PTL.ExternalWeb.ViewSupport;

// Back links from a detail page return to the list URL (with its filter, sort and page) that opened it.
// The value comes from the query string, so only a same-site relative URL for the expected list page is honoured.
public static class ListReturnUrl
{
    public const string DistributionPrevious = "/Distribution/Previous";
    public const string TabulationIndex = "/Tabulation/Index";
    public const string CommentsIndex = "/Comments/Index";

    // The list page a breadcrumb step points at, when that step is one of the filterable lists.
    public static string? ListPathFor(string? controller, string? action) =>
        (controller, action) switch
        {
            ("Distribution", "Previous") => DistributionPrevious,
            ("Tabulation", "Index") => TabulationIndex,
            ("Comments", "Index") => CommentsIndex,
            _ => null
        };

    public static string? Validate(string? returnUrl, string listPath)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || returnUrl.Length > 2048
            || returnUrl[0] != '/'
            || returnUrl.Length > 1 && (returnUrl[1] == '/' || returnUrl[1] == '\\')
            || returnUrl.Any(char.IsControl)
            || returnUrl.Contains('\\', StringComparison.Ordinal))
        {
            return null;
        }

        var pathEnd = returnUrl.IndexOfAny(['?', '#']);
        var path = pathEnd < 0 ? returnUrl : returnUrl[..pathEnd];
        return path.EndsWith(listPath, StringComparison.OrdinalIgnoreCase) ? returnUrl : null;
    }
}
