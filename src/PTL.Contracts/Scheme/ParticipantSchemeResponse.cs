namespace PTL.Contracts.Scheme;

// Participant-facing read model for GET /api/schemes/participant/{participantId} - deliberately
// not SchemeResponse (that's the full ~70-field Scheme Core admin shape). Mirrors legacy
// SchemeList.aspx's two grids exactly: every commercially-available scheme for the current year
// (with a computed IsParticipating flag) plus every non-commercial scheme this participant is
// actually linked to for that year. Named "Summary" (not ParticipantSchemeResponse) to avoid
// ambiguity with PTL.Contracts.Participant.ParticipantSchemeResponse (the contract-order concept).
public sealed record ParticipantSchemeSummaryResponse(Guid SchemeId, string Identifier, string Name, string Year, bool IsParticipating);

public sealed record ParticipantSchemeListResponse(
    IReadOnlyList<ParticipantSchemeSummaryResponse> CommercialSchemes,
    IReadOnlyList<ParticipantSchemeSummaryResponse> NonCommercialSchemes);
