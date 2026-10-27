using PTL.Contracts.Scheme;

namespace PTL.ExternalWeb.Features.Scheme;

/// <summary>View model for the participant-facing Scheme List screen (legacy SchemeList.aspx).
/// Only the commercial schemes table is paginated (legacy GridView paging) - the non-commercial
/// table only ever shows the schemes this participant is actually linked to, never large enough
/// to need paging. Both tables are independently sortable (legacy GridView SortExpression on
/// SchemeCode/SchemeName/Year on both grids).</summary>
public sealed record SchemeListViewModel(
    IReadOnlyList<ParticipantSchemeSummaryResponse> CommercialSchemes,
    int CommercialSchemeTotalCount,
    int Page,
    int PageSize,
    string SortBy,
    string SortDirection,
    IReadOnlyList<ParticipantSchemeSummaryResponse> NonCommercialSchemes,
    string NonCommercialSortBy,
    string NonCommercialSortDirection);
