using PTL.Contracts.Scheme;

namespace PTL.ExternalWeb.Features.Scheme;

/// <summary>View model for the participant-facing Scheme List screen (legacy SchemeList.aspx).</summary>
public sealed record SchemeListViewModel(
    IReadOnlyList<ParticipantSchemeSummaryResponse> CommercialSchemes,
    IReadOnlyList<ParticipantSchemeSummaryResponse> NonCommercialSchemes);
