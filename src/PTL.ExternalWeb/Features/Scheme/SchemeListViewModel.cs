using PTL.Contracts.Scheme;

namespace PTL.ExternalWeb.Features.Scheme;

/// <summary>View model for the participant-facing Scheme List screen (legacy SchemeList.aspx).
/// Only the commercial schemes table is paginated (legacy GridView paging) - the non-commercial
/// table only ever shows the schemes this participant is actually linked to, never large enough
/// to need paging.</summary>
public sealed record SchemeListViewModel(
    IReadOnlyList<ParticipantSchemeSummaryResponse> CommercialSchemes,
    int CommercialSchemeTotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<ParticipantSchemeSummaryResponse> NonCommercialSchemes);
