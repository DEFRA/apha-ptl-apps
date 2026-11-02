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
