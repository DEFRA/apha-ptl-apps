namespace PTL.SharedUI.Pagination;

/// <summary>
/// Shared paging state for every list page (Customer/Participant/Contract/Scheme/future) - built by
/// each page's controller/view from its own search-result totals, then rendered by
/// <c>govuk-page-size-selector</c> (above the table) and <c>govuk-pagination</c> (below the table).
/// <see cref="RouteValues"/> must contain every OTHER query parameter the page needs to preserve
/// (search term, status filter, sort order, etc.) - the page and page-size values are supplied by the
/// components themselves (as <see cref="PageParameter"/> and <see cref="PageSizeParameter"/>) and must not be included here.
/// </summary>
public sealed class PaginationModel
{
    public int CurrentPage { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public int TotalRecords { get; init; }

    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>The controller action the pagination/page-size links post/navigate back to - defaults to the current list page's own "Index".</summary>
    public string Action { get; init; } = "Index";

    /// <summary>Query-string name of the page number - overridden only when one page holds two independently paged tables.</summary>
    public string PageParameter { get; init; } = "page";

    /// <summary>Query-string name of the page size, also used as the id of the page-size dropdown (so it must be unique on the page).</summary>
    public string PageSizeParameter { get; init; } = "pageSize";

    /// <summary>Every filter/sort query value to preserve across a page or page-size change, excluding "page" and "pageSize".</summary>
    public IReadOnlyDictionary<string, object?> RouteValues { get; init; } = new Dictionary<string, object?>();

    public const int DefaultPageSize = 25;

    // 15 added for PTL.ExternalWeb's Scheme List (legacy GridView page size) alongside InternalWeb's
    // existing choices - purely additive, no existing screen's selected size is removed.
    public static IReadOnlyList<int> AvailablePageSizes { get; } = [10, 15, 20, 25, 50, 100];
}
