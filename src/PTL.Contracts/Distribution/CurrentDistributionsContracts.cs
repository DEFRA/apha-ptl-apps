namespace PTL.Contracts.Distribution;

// External participant Enter Results list (legacy ExternalWeb/DistributionsCurrent.aspx).
public sealed record CurrentDistributionResponse(
    Guid MonthlyDistributionSchemeId,
    Guid DistributionParticipantId,
    string DistributionReference,
    string SchemeCode,
    string SchemeName,
    DateTime DistributionDate,
    DateTime DeadlineDate,
    bool HasResults,
    bool Submitted);
