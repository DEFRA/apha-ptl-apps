namespace PTL.ExternalWeb.Features.Scheme;

// Bound from the query string (names are matched case-insensitively, so ?ncPage=2 binds to NcPage).
public sealed class SchemeListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = SchemeController.DefaultPageSize;
    public string SortBy { get; init; } = "code";
    public string SortDir { get; init; } = "asc";
    public string NcSortBy { get; init; } = "code";
    public string NcSortDir { get; init; } = "asc";
    public int NcPage { get; init; } = 1;
    public int NcPageSize { get; init; } = SchemeController.DefaultPageSize;
}
