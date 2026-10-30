using Microsoft.AspNetCore.Html;

namespace PTL.ExternalWeb.ViewSupport;

// GOV.UK ships no sortable-table-header component - this hand-rolled, accessible pattern
// (aria-sort on the th, a visible glyph plus a visually-hidden direction label alongside the
// column link) was duplicated across Scheme/Distribution/Tabulation's Index views; shared here
// instead of a fourth copy.
public static class SortableColumnHelpers
{
    public static string AriaSort(string currentSortBy, string currentSortDir, string column) =>
        currentSortBy != column ? "none" : currentSortDir == "asc" ? "ascending" : "descending";

    public static IHtmlContent SortIndicator(string currentSortBy, string currentSortDir, string column)
    {
        if (currentSortBy != column)
        {
            return HtmlString.Empty;
        }

        var arrow = currentSortDir == "asc" ? " \u25b2" : " \u25bc";
        var direction = currentSortDir == "asc" ? "ascending" : "descending";
        return new HtmlString($"""<span aria-hidden="true">{arrow}</span><span class="govuk-visually-hidden">, sorted {direction}</span>""");
    }
}
