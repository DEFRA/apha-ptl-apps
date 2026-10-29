namespace PTL.Core.Distribution;

// External Enter Results -> Current Distributions list row.
public sealed record CurrentDistributionData(
    Guid MonthlyDistributionSchemeId,
    Guid DistributionParticipantId,
    string DistributionReference,
    string SchemeCode,
    string SchemeName,
    DateTime DistributionDate,
    DateTime DeadlineDate,
    bool HasResults,
    bool Submitted);
