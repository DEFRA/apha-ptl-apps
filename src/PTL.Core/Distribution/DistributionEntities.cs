namespace PTL.Core.Distribution;

// spgaMonthlyDistributionInfo - one row per month that already has an initialised
// tblMonthlyDistribution row. Raw counts only; legacy MonthlyDistributionInfo.vb derives the
// per-stage completion percentages from these at read time - see DistributionMonthSummaryExtensions.
public class DistributionMonthSummaryEntity
{
    public Guid MonthlyDistributionId { get; set; }
    public int YearId { get; set; }
    public int MonthId { get; set; }
    public int NumberOfSchemes { get; set; }
    public int NumberOfSampleNumbersDefined { get; set; }
    public int NumberOfPrepComplete { get; set; }
    public int NumberOfParticipants { get; set; }
    public int NumberOfPackagingComplete { get; set; }
    public int NumberOfResultsEntered { get; set; }
    public int NumberOfTabulations { get; set; }
    public int NumberOfCompleteTabulations { get; set; }
}

// spgaDistributionYearInfo - one row per distinct calendar year/month that has an initialised
// distribution with at least one still-relevant scheme (see DistributionYearInfoCollection.vb).
// A plain settable-property class, not a record - Dapper's CustomPropertyTypeMap requires a
// parameterless constructor to materialise from (a positional record's generated ctor doesn't
// match the result columns' fld-prefixed names, so Dapper falls back to constructor-matching and
// throws).
public class DistributionMonthYearEntity
{
    public int YearId { get; set; }
    public int MonthId { get; set; }
}
