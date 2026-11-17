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

/// <summary>Legacy TabulationView.aspx - the published tabulation itself. Tests holds the accordion
/// sections built from the report; null keeps the one-table-per-test layout.</summary>
public sealed record TabulationReportPageViewModel(
    TabulationReportResponse Report,
    string? ReturnUrl = null,
    IReadOnlyList<TabulationTestSections>? Tests = null);

public sealed record TabulationTestSections(string TestName, Guid? AssessmentTestId, IReadOnlyList<TabulationAccordionSection> Sections);

public sealed record TabulationAccordionSection(string Title, TabulationTableResponse Table);

public sealed record TabulationAccordionViewModel(string Id, IReadOnlyList<TabulationAccordionSection> Sections);

/// <summary>Legacy AssessmentResults.aspx - the information list, the bar chart and its data table.
/// Details splits the builder's "Label: value" titles (after the intro sentence); legacy ExternalWeb
/// calls the tabulation "Report".</summary>
public sealed record AssessmentResultsPageViewModel(Guid PublishedTabulationId, Guid TestId, AssessmentResultsResponse Results, string? ReturnUrl = null)
{
    public IReadOnlyList<(string Label, string Value)> Details { get; } =
        [.. Results.Titles.Skip(1).Where(t => !string.IsNullOrWhiteSpace(t)).Select(SplitTitle)];

    private static (string Label, string Value) SplitTitle(string title)
    {
        var separator = title.IndexOf(": ", StringComparison.Ordinal);
        if (separator < 0)
        {
            return (string.Empty, title.Trim());
        }

        var label = title[..separator];
        return (label == "Tabulation" ? "Report" : label, title[(separator + 2)..].Trim());
    }
}
