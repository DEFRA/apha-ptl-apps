namespace PTL.Core.Scheme;

// Keyless domain projection for the list returned by spgSchemeInfoByYearId. Field set matches
// PtaBusinessObjects SchemeInfo exactly (see docs/analysis/scheme-analysis.md).
public class SchemeSummaryEntity
{
    public Guid SharedId { get; set; }
    public int YearId { get; set; }
    public Guid? CurrentSchemeId { get; set; }
    public string? CurrentIdentifier { get; set; }
    public string? CurrentName { get; set; }
    public Guid? NextSchemeId { get; set; }
    public string? NextIdentifier { get; set; }
    public string? NextName { get; set; }
    public Guid? RecentSchemeId { get; set; }
    public string? RecentIdentifier { get; set; }
    public string? RecentName { get; set; }
}
