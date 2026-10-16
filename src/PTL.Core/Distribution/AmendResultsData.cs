namespace PTL.Core.Distribution;

// spgMonthlyDistributionSchemeForAmendResults - legacy AmendedResults.MonthlyDistributionScheme.
// Participants are keyed by DistributionParticipantId (the procedure's child fldParticipantId).
public sealed class AmendResultsSource
{
    public Guid MonthlyDistributionSchemeId { get; init; }
    public string SchemeName { get; init; } = string.Empty;
    public string SchemeIdentifier { get; init; } = string.Empty;
    public string DistributionReference { get; init; } = string.Empty;
    public string DistributionReferenceSuffix { get; init; } = string.Empty;
    public string ScheduleCode { get; init; } = string.Empty;
    public bool StoreRatings { get; init; }
    public bool RequiresAssessment { get; init; }
    public List<AmendSourceTest> Tests { get; } = [];
    public List<AmendSourceParticipant> Participants { get; } = [];
    public Dictionary<Guid, string> IntendedMethodResults { get; } = [];
    public Dictionary<(Guid SampleId, Guid ResultItemId), (string Value, string Value2)> IntendedResultItemResults { get; } = [];

    public string DistributionReferenceFull => $"{DistributionReference}{DistributionReferenceSuffix}/{ScheduleCode}";
}

public sealed class AmendSourceTest
{
    public Guid TestId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsAssessmentSubmitted { get; init; }
    public List<AmendSourceItem> MethodItems { get; } = [];
    public List<AmendSourceItem> ResultItems { get; } = [];
    public List<TabulationSourceItem> CategoryItems { get; } = [];
    public List<AmendSourceSample> Samples { get; } = [];
}

// ResultItemTypeId/Order are only needed to write IsRange back through spuDistributionTestResultItem.
public sealed record AmendSourceItem(Guid ItemId, string Name, string FormatString, string Message, bool IsRange = false, Guid ResultItemTypeId = default, int Order = 0);

public sealed record AmendSourceSample(Guid SampleId, string SampleNumber);

public sealed class AmendSourceParticipant
{
    public Guid DistributionParticipantId { get; init; }
    public string LabCode { get; init; } = string.Empty;
    public string NilReturnReason { get; init; } = string.Empty;
    public Dictionary<Guid, string> MethodResults { get; } = [];
    public Dictionary<(Guid SampleId, Guid ResultItemId), string> ResultItemResults { get; } = [];
    public Dictionary<(Guid SampleId, Guid CategoryItemId), (int Score, Guid SampleTestId)> CategoryResults { get; } = [];
    public Dictionary<(Guid TestId, Guid SampleId), (Guid SampleReceiptId, string Comment, int Score)> Assessments { get; } = [];
    public Dictionary<Guid, (Guid SampleRatingId, Guid RatingId)> Ratings { get; } = [];
}

public sealed record AmendRatingOption(Guid RatingId, string Rating);

// One Save, written in the legacy DataPortal_Update order inside a single transaction. AuditWhy is
// empty when nothing auditable changed (legacy only went through Audit.aspx when IsAuditDirty).
public sealed record AmendResultsWrite(
    string AuditWho,
    string AuditWhy,
    IReadOnlyList<AmendRangeWrite> Ranges,
    IReadOnlyList<AmendParticipantWrite> Participants,
    IReadOnlyList<AmendIntendedResultWrite> IntendedResults,
    IReadOnlyList<AmendIntendedMethodWrite> IntendedMethods);

public sealed record AmendRangeWrite(Guid ResultItemId, Guid TestId, Guid ResultItemTypeId, int Order, bool IsRange);

public sealed record AmendParticipantWrite(
    Guid DistributionParticipantId,
    string NilReturnReason,
    IReadOnlyList<AmendMethodResultWrite> MethodResults,
    IReadOnlyList<AmendResultItemWrite> ResultItemResults,
    IReadOnlyList<AmendAssessmentWrite> Assessments,
    IReadOnlyList<AmendRatingWrite> Ratings,
    IReadOnlyList<AmendCategoryWrite> CategoryResults);

public sealed record AmendMethodResultWrite(Guid TestId, Guid MethodItemId, string Value);

public sealed record AmendResultItemWrite(Guid TestId, Guid SampleId, Guid ResultItemId, string Value);

public sealed record AmendAssessmentWrite(Guid SampleReceiptId, string Comment, bool CommentChanged, int Score, bool ScoreChanged);

public sealed record AmendRatingWrite(Guid SampleRatingId, Guid RatingId);

public sealed record AmendCategoryWrite(Guid TestId, Guid SampleId, Guid CategoryItemId, Guid SampleTestId, int Score);

public sealed record AmendIntendedResultWrite(Guid TestId, Guid SampleId, Guid ResultItemId, string Value, bool ValueChanged, string Value2, bool Value2Changed);

public sealed record AmendIntendedMethodWrite(Guid TestId, Guid MethodItemId, string Value);
