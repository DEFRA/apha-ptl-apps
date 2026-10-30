namespace PTL.Core.Distribution;

// spgTabulations - legacy ExternalWeb Tabulations.aspx (TabulationCollection.
// FetchTabulationCollection) - every published tabulation a participant/viewer/test consultant is
// authorised to view. A plain settable-property class, not a record - Dapper's default mapper
// needs a parameterless constructor to materialise rows this way (see DapperColumnMappings).
public class PublishedTabulationSummaryEntity
{
    // This is actually the PublishedTabulationId - the SP joins through tblPublishedTabulation and
    // aliases its key as fldTabulationId, matching legacy's DistributionsPast-style naming.
    public Guid TabulationId { get; set; }

    public string DistributionReference { get; set; } = string.Empty;

    // Already "{Identifier}: {Name}", concatenated by the stored procedure - split below exactly as
    // legacy PastDistribution.vb's SchemeCode/SchemeNameOnly derived properties did.
    public string SchemeName { get; set; } = string.Empty;

    public string TabulationName { get; set; } = string.Empty;
    public DateTime DistributionDate { get; set; }
    public int YearId { get; set; }
    public int MonthId { get; set; }

    public string SchemeCode => SplitSchemeName()[0];

    public string SchemeNameOnly => SplitSchemeName()[1];

    private string[] SplitSchemeName() =>
        !string.IsNullOrEmpty(SchemeName) && SchemeName.Contains(':')
            ? [SchemeName[..SchemeName.IndexOf(':')].Trim(), SchemeName[(SchemeName.IndexOf(':') + 1)..].Trim()]
            : [SchemeName, SchemeName];
}
