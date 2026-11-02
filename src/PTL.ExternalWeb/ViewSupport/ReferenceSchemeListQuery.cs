namespace PTL.ExternalWeb.ViewSupport;

// Query-string filter/sort/paging values shared by Distribution/Previous and Tabulation/Index; bound
// as one object (property names match the existing query keys, so URLs are unchanged).
public sealed class ReferenceSchemeListQuery
{
    public const int DefaultPageSize = 15;

    public int? Year { get; init; }

    public int? Month { get; init; }

    public string? DistributionReference { get; init; }

    public string? SchemeCode { get; init; }

    public string? SchemeName { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public string SortBy { get; init; } = "distributionDate";

    public string SortDir { get; init; } = "desc";
}
