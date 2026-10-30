namespace PTL.Core.Distribution;

// Previous Distributions (legacy ExternalWeb DistributionsPrevious.aspx) - a plain settable-property
// class, not a record, since the repository SQL (EXEC spgPastDistributionsBySsoId) returns raw
// fld-prefixed columns mapped via DapperColumnMappings and Dapper's default mapper needs a
// parameterless constructor to materialise rows that way.
public class ParticipantPastDistributionEntity
{
    public Guid MonthlyDistributionSchemeId { get; set; }

    // Already "{Reference}{Suffix}/{ScheduleCode}", concatenated by the stored procedure.
    public string DistributionReference { get; set; } = string.Empty;

    // Already "{Identifier}: {Name}", concatenated by the stored procedure - split below exactly as
    // legacy PastDistribution.vb's SchemeCode/SchemeNameOnly derived properties did.
    public string SchemeName { get; set; } = string.Empty;

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
