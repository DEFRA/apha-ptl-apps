namespace PTL.Core.Distribution;

// spgAssessmentEntry and spgParticipantResultForAssessment: one participant's submission of one sample, the test's category items
// with their criteria, and what has been saved so far.
public sealed class AssessorEntryEntity
{
    public Guid SampleReceiptId { get; init; }
    public Guid TestId { get; init; }
    public int YearId { get; init; }
    public int MonthId { get; init; }
    public string SchemeName { get; init; } = string.Empty;
    public string SchemeIdentifier { get; init; } = string.Empty;
    public string DistributionReference { get; init; } = string.Empty;
    public string DistributionReferenceSuffix { get; init; } = string.Empty;
    public string ScheduleCode { get; init; } = string.Empty;
    public string TestName { get; init; } = string.Empty;
    public string LabCode { get; init; } = string.Empty;
    public string SampleNumber { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public int Score { get; init; }
    public Guid AssessorId { get; init; }
    public string AssessorName { get; init; } = string.Empty;
    public DateTime? AssessmentSubmitted { get; init; }
    public List<AssessorCategoryEntity> Categories { get; } = [];
    public List<AssessorResultItemEntity> ResultItems { get; } = [];
}

// ActualScore is null until the category has been saved for this participant (legacy then inserts it rather than updating it).
public sealed class AssessorCategoryEntity
{
    public Guid CategoryItemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? ActualScore { get; init; }
    public List<AssessorCriterionEntity> Criteria { get; } = [];
}

public sealed record AssessorCriterionEntity(Guid CriterionItemId, string Name, bool IsSelected);

public sealed record AssessorResultItemEntity(string Name, string Result);

// What one save writes. AssessmentDate is when it is saved (legacy stamped DateTime.Now, local time).
public sealed record AssessorEntrySave(
    Guid SampleTestId,
    Guid ParticipantId,
    Guid AssessorUserId,
    string Comment,
    int OverallScore,
    IReadOnlyList<AssessorCategorySave> Categories,
    DateTime AssessmentDate);

public sealed record AssessorCategorySave(Guid CategoryItemId, int Score, IReadOnlyList<Guid> CriterionItemIds);

public enum AssessorSaveOutcome
{
    Saved,
    NotFound,

    // The user is not one of the scheme's Assessors.
    Forbidden,

    // The test has been submitted, so its assessment can no longer change.
    Submitted,

    // The request does not match the test's categories or their scores, or the comment is too long.
    Invalid,
}

public sealed record AssessorValidationError(string Key, string Message);

public enum AssessorSubmitOutcome
{
    Submitted,
    NotFound,

    // The user is not one of the scheme's Assessors.
    Forbidden,

    // The test has already been submitted; its assessment is final.
    AlreadySubmitted,
}

public sealed record AssessorSaveResult(AssessorSaveOutcome Outcome, IReadOnlyList<AssessorValidationError> Errors)
{
    public static AssessorSaveResult Of(AssessorSaveOutcome outcome) => new(outcome, []);
}
