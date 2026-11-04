namespace PTL.ExternalWeb.ViewSupport;

public sealed record SortableHeader(string Label, string Column, string SortBy, string SortDirection, string Url);

// Binds a list page's current sort state and SortUrl function so each column header is a one-liner.
public sealed record SortableHeaderContext(string SortBy, string SortDirection, Func<string, string> SortUrl)
{
    public SortableHeader Header(string label, string column) => new(label, column, SortBy, SortDirection, SortUrl(column));
}
