using PTL.Contracts.Distribution;

namespace PTL.ExternalWeb.Features.Tabulation;

/// <summary>View model for the "View Reports" list screen (legacy Tabulations.aspx). Search/sort/
/// pagination are all applied in-memory by the controller over one full fetch per request, the
/// same pattern as Features/Distribution/DistributionController.Previous.</summary>
public sealed record TabulationListViewModel(
    IReadOnlyList<PublishedTabulationSummaryResponse> Tabulations,
    int TotalCount,
    int Page,
    int PageSize,
    string SortBy,
    string SortDirection,
    int? Year,
    int? Month,
    string? DistributionReference,
    string? SchemeCode,
    string? SchemeName,
    IReadOnlyList<int> AvailableYears)
{
    public bool IsFiltered =>
        Year is not null || Month is not null
        || !string.IsNullOrWhiteSpace(DistributionReference)
        || !string.IsNullOrWhiteSpace(SchemeCode)
        || !string.IsNullOrWhiteSpace(SchemeName);
}

/// <summary>Legacy TabulationView.aspx - the published tabulation itself.</summary>
public sealed record TabulationReportPageViewModel(TabulationReportResponse Report);

/// <summary>Legacy AssessmentResults.aspx - rendered as a data table rather than the legacy bar
/// chart (not required by this story's acceptance criteria).</summary>
public sealed record AssessmentResultsPageViewModel(Guid PublishedTabulationId, Guid TestId, AssessmentResultsResponse Results);
