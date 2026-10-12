namespace PTL.Core.Distribution;

// spgMonthlyDistribution's scheme result set, joined with the aggregated participant result set -
// legacy PtaBusinessObjects.BusinessObjects.Distributions.MonthlyDistributionScheme. Carries every
// column the save path (spuMonthlyDistributionScheme) requires, including the fields the
// Scheduling screen never edits (Comments/HasIntendedResults/StoreRatings/SchemeVersionDate) -
// these must be round-tripped unchanged on every save, exactly like the legacy CSLA object does.
public class MonthlyDistributionSchemeEntity
{
    public Guid MonthlyDistributionSchemeId { get; set; }
    public Guid MonthlyDistributionId { get; set; }
    public Guid SchemeId { get; set; }
    public string SchemeIdentifier { get; set; } = string.Empty;
    public string SchemeName { get; set; } = string.Empty;
    public string DistributionReference { get; set; } = string.Empty;
    public string DistributionReferenceSuffix { get; set; } = string.Empty;
    public string ScheduleCode { get; set; } = string.Empty;
    public DateTime DistributionDate { get; set; }
    public DateTime OverseasPostingDate { get; set; }
    public DateTime DeadlineDate { get; set; }
    public DateTime ResultsIssueTargetDate { get; set; }
    public string Comments { get; set; } = string.Empty;
    public bool HasIntendedResults { get; set; }
    public bool HasSampleNumbersDefined { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsAsAvailable { get; set; }
    public bool StoreRatings { get; set; }
    public DateTime SchemeVersionDate { get; set; }
    public int ParticipantCount { get; set; }
    public int TotalSetsOfSamplesRequired { get; set; }

    // Legacy MonthlyDistributionScheme.vb: Reference + Suffix + "/" + ScheduleCode (e.g. "17152/BA").
    public string DistributionReferenceFull => $"{DistributionReference}{DistributionReferenceSuffix}/{ScheduleCode}";
}

// spgMonthlyDistribution result set 3 (participants) - only the two columns needed to compute
// ParticipantCount/TotalSetsOfSamplesRequired per scheme (legacy's NumberOfSetsRequired property
// sums DistributionParticipant.NumberOfSetsRequired over the REGULAR participants collection
// only, not AdditionalParticipants - see distribution-migration.md).
public class MonthlyDistributionParticipantAggregateEntity
{
    public Guid DistributionSchemeId { get; set; }
    public int NumberOfSetsRequired { get; set; }
}
