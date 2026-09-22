namespace PTL.Contracts.Scheme;

// Public API contract for the list projection returned by spgSchemeInfoByYearId - backs the
// Scheme list/search screen (SchemeList.aspx). Current/Next/Recent triples mirror the legacy
// SchemeInfo shape exactly (see docs/analysis/scheme-analysis.md).
public sealed record SchemeSummaryResponse(
    Guid SharedId,
    int YearId,
    Guid? CurrentSchemeId,
    string? CurrentIdentifier,
    string? CurrentName,
    Guid? NextSchemeId,
    string? NextIdentifier,
    string? NextName,
    Guid? RecentSchemeId,
    string? RecentIdentifier,
    string? RecentName);
