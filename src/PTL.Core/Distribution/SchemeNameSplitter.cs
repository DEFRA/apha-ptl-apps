namespace PTL.Core.Distribution;

// Shared by ParticipantPastDistributionEntity and PublishedTabulationSummaryEntity: both map a
// stored procedure's single "{Identifier}: {Name}" SchemeName column, split exactly as legacy
// PastDistribution.vb's SchemeCode/SchemeNameOnly derived properties did.
public static class SchemeNameSplitter
{
    public static (string Code, string NameOnly) Split(string schemeName) =>
        !string.IsNullOrEmpty(schemeName) && schemeName.Contains(':')
            ? (schemeName[..schemeName.IndexOf(':')].Trim(), schemeName[(schemeName.IndexOf(':') + 1)..].Trim())
            : (schemeName, schemeName);
}
