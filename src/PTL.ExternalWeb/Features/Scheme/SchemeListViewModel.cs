using PTL.Contracts.Scheme;

namespace PTL.ExternalWeb.Features.Scheme;

/// <summary>View model for the participant-facing Scheme List screen (legacy SchemeList.aspx).
/// Both tables page and sort independently, as the legacy grids did: each has its own page, page
/// size and sort (legacy GridView SortExpression on SchemeCode/SchemeName/Year on both grids).</summary>
public sealed record SchemeListViewModel(
    IReadOnlyList<ParticipantSchemeSummaryResponse> CommercialSchemes,
    int CommercialSchemeTotalCount,
    int Page,
    int PageSize,
    string SortBy,
    string SortDirection,
    IReadOnlyList<ParticipantSchemeSummaryResponse> NonCommercialSchemes,
    string NonCommercialSortBy,
    string NonCommercialSortDirection,
    int NonCommercialSchemeTotalCount,
    int NonCommercialPage,
    int NonCommercialPageSize);
