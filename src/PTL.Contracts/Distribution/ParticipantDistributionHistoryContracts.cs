namespace PTL.Contracts.Distribution;

// Previous Distributions (legacy ExternalWeb DistributionsPrevious.aspx, backed by the existing
// spgPastDistributionsBySsoId stored procedure) - every distribution a participant has received
// posted results for. No DistributionParticipantId - legacy's own PastDistribution.vb does not
// carry it either; the Results screen resolves it from (participantId, monthlyDistributionSchemeId)
// server-side instead (see ActualResultsService.GetActualResultsForParticipantAsync).
public sealed record ParticipantPastDistributionResponse(
    Guid MonthlyDistributionSchemeId,
    string DistributionReference,
    string SchemeCode,
    string SchemeName,
    DateTime DistributionDate,
    int YearId,
    int MonthId);

public sealed record ParticipantPastDistributionListResponse(IReadOnlyList<ParticipantPastDistributionResponse> Distributions);
