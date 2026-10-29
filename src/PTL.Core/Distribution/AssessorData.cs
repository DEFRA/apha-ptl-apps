namespace PTL.Core.Distribution;

// What the Assessor pages read: spgYearByAssessorId, spgMonthlyDistributionInfoForAssessor and spgAssessmentItemByTestId.
public sealed class AssessorMonthEntity
{
    public int YearId { get; init; }
    public int MonthId { get; init; }
    public List<AssessorDistributionEntity> Distributions { get; } = [];
}

public sealed class AssessorDistributionEntity
{
    public Guid MonthlyDistributionSchemeId { get; init; }
    public string SchemeName { get; init; } = string.Empty;
    public string SchemeIdentifier { get; init; } = string.Empty;
    public string DistributionReference { get; init; } = string.Empty;
    public string DistributionReferenceSuffix { get; init; } = string.Empty;
    public string ScheduleCode { get; init; } = string.Empty;
    public List<AssessorTestEntity> Tests { get; } = [];
}

public sealed record AssessorTestEntity(Guid TestId, string TestType, DateTime? AssessmentSubmitted);

public sealed class AssessorItemListEntity
{
    public int YearId { get; init; }
    public int MonthId { get; init; }
    public string SchemeName { get; init; } = string.Empty;
    public string SchemeIdentifier { get; init; } = string.Empty;
    public string DistributionReference { get; init; } = string.Empty;
    public string DistributionReferenceSuffix { get; init; } = string.Empty;
    public string ScheduleCode { get; init; } = string.Empty;
    public string TestName { get; init; } = string.Empty;
    public DateTime? AssessmentSubmitted { get; init; }
    public string AssessorName { get; init; } = string.Empty;
    public List<AssessorSampleEntity> Samples { get; } = [];
}

public sealed class AssessorSampleEntity
{
    public Guid SampleId { get; init; }
    public string SampleNumber { get; init; } = string.Empty;
    public List<AssessorItemEntity> Items { get; } = [];
}

public sealed record AssessorItemEntity(Guid SampleTestId, Guid ParticipantId, string LabCode, DateTime? AssessmentDate);
