namespace PTL.Contracts.Distribution;

// Tabulations (legacy ExternalWeb Tabulations.aspx) - every published tabulation a participant,
// viewer or test consultant is authorised to view, across all schemes and years.
public sealed record PublishedTabulationSummaryResponse(
    Guid TabulationId,
    string DistributionReference,
    string SchemeCode,
    string SchemeName,
    string TabulationName,
    DateTime DistributionDate,
    int YearId,
    int MonthId);

public sealed record PublishedTabulationSummaryListResponse(IReadOnlyList<PublishedTabulationSummaryResponse> Tabulations);

// The signed-in external user a "for-user" tabulation request is scoped to: the CIDM contact id (fldSsoIdExt) and, for a
// Participant or Lab User, the selected lab. ViewerId only picks the viewer's own view of a report, never access.
public sealed record ExternalTabulationCaller(Guid? SsoIdExt, Guid? ParticipantId, Guid? ViewerId);
