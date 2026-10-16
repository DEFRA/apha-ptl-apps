namespace PTL.Contracts.Distribution;

// Amend Results (legacy Scheduling/AmendResults.aspx, sitemap "Amend Results").
public sealed record AmendResultsResponse(
    Guid MonthlyDistributionSchemeId,
    string DistributionTitle,
    bool RequiresAssessment,
    bool StoreRatings,
    IReadOnlyList<AmendTestResponse> Tests,
    IReadOnlyList<AmendParticipantResponse> Participants,
    IReadOnlyList<AmendRatingOptionResponse> RatingOptions,
    IReadOnlyList<AmendSampleResponse> RatingSamples,
    IReadOnlyList<AmendSampleStatisticsResponse> Statistics);

public sealed record AmendTestResponse(
    Guid TestId,
    string Name,
    bool IsAssessmentSubmitted,
    IReadOnlyList<AmendItemResponse> MethodItems,
    IReadOnlyList<AmendItemResponse> ResultItems,
    IReadOnlyList<AmendCategoryItemResponse> CategoryItems,
    IReadOnlyList<AmendSampleResponse> Samples,
    IReadOnlyList<AmendIntendedValueResponse> IntendedMethodResults,
    IReadOnlyList<AmendIntendedValueResponse> IntendedResultItemResults);

// FormatString starting "^" = free text checked against the regex; otherwise a "|" list of options.
// Message = the legacy "Input must be of format: {Message}" text.
public sealed record AmendItemResponse(Guid ItemId, string Name, string FormatString, string Message, bool IsRange);

public sealed record AmendCategoryItemResponse(Guid CategoryItemId, string Name);

public sealed record AmendSampleResponse(Guid SampleId, string SampleNumber);

// SampleId is Guid.Empty for method items.
public sealed record AmendIntendedValueResponse(Guid ItemId, Guid SampleId, string Value, string Value2);

public sealed record AmendParticipantResponse(
    Guid DistributionParticipantId,
    string LabCode,
    string NilReturnReason,
    bool HasResults,
    bool HasRatingResults,
    IReadOnlyList<AmendParticipantValueResponse> MethodResults,
    IReadOnlyList<AmendParticipantValueResponse> ResultItemResults,
    IReadOnlyList<AmendParticipantScoreResponse> CategoryResults,
    IReadOnlyList<AmendAssessmentResponse> Assessments,
    IReadOnlyList<AmendRatingResponse> Ratings);

// SampleId is Guid.Empty for method items.
public sealed record AmendParticipantValueResponse(Guid TestId, Guid ItemId, Guid SampleId, string Value);

public sealed record AmendParticipantScoreResponse(Guid TestId, Guid CategoryItemId, Guid SampleId, int Score);

public sealed record AmendAssessmentResponse(Guid TestId, Guid SampleId, string Comment, int Score);

public sealed record AmendRatingResponse(Guid SampleId, Guid RatingId);

public sealed record AmendRatingOptionResponse(Guid RatingId, string Rating);

// Legacy CalculateStats() output for one sample (strings exactly as the page showed them).
public sealed record AmendSampleStatisticsResponse(
    Guid SampleId, string MeanScore, string LabsReporting, string ParticipantCount, string PercentageCorrect);

// The whole posted grid; the service works out what changed against the stored values.
public sealed record AmendResultsSaveRequest(
    IReadOnlyList<AmendIntendedValueRequest> IntendedMethodResults,
    IReadOnlyList<AmendIntendedValueRequest> IntendedResultItemResults,
    IReadOnlyList<AmendRangeRequest> Ranges,
    IReadOnlyList<AmendParticipantValueRequest> MethodResults,
    IReadOnlyList<AmendParticipantValueRequest> ResultItemResults,
    IReadOnlyList<AmendParticipantScoreRequest> CategoryResults,
    IReadOnlyList<AmendAssessmentRequest> Assessments,
    IReadOnlyList<AmendNilReturnRequest> NilReturns,
    IReadOnlyList<AmendRatingRequest> Ratings,
    string? AuditWhy = null);

public sealed record AmendIntendedValueRequest(Guid ItemId, Guid SampleId, string? Value, string? Value2);

public sealed record AmendRangeRequest(Guid ResultItemId, bool IsRange);

public sealed record AmendParticipantValueRequest(Guid DistributionParticipantId, Guid ItemId, Guid SampleId, string? Value);

public sealed record AmendParticipantScoreRequest(Guid DistributionParticipantId, Guid CategoryItemId, Guid SampleId, int Score);

public sealed record AmendAssessmentRequest(Guid DistributionParticipantId, Guid TestId, Guid SampleId, string? Comment, int Score);

public sealed record AmendNilReturnRequest(Guid DistributionParticipantId, string? NilReturnReason);

public sealed record AmendRatingRequest(Guid DistributionParticipantId, Guid SampleId, Guid RatingId);

// FieldErrors are keyed with AmendResultsFieldKeys so the page can put each message on its input.
public sealed record AmendResultsSaveResult(
    bool Success,
    bool AuditReasonRequired,
    string AuditObjectName,
    IReadOnlyDictionary<string, string> FieldErrors);

public static class AmendResultsFieldKeys
{
    public static string IntendedMethod(Guid methodItemId) => $"im_{methodItemId:N}";

    public static string IntendedResult(Guid sampleId, Guid resultItemId, int index) => $"ir{index}_{sampleId:N}_{resultItemId:N}";

    public static string ParticipantMethod(Guid participantId, Guid methodItemId) => $"pm_{participantId:N}_{methodItemId:N}";

    public static string ParticipantResult(Guid participantId, Guid sampleId, Guid resultItemId) => $"pr_{participantId:N}_{sampleId:N}_{resultItemId:N}";

    public static string AssessmentComment(Guid participantId, Guid testId, Guid sampleId) => $"ac_{participantId:N}_{testId:N}_{sampleId:N}";

    public static string AssessmentScore(Guid participantId, Guid testId, Guid sampleId) => $"as_{participantId:N}_{testId:N}_{sampleId:N}";

    public static string CategoryScore(Guid participantId, Guid sampleId, Guid categoryItemId) => $"pc_{participantId:N}_{sampleId:N}_{categoryItemId:N}";

    public static string Rating(Guid participantId, Guid sampleId) => $"rr_{participantId:N}_{sampleId:N}";

    // The checkbox only posts when ticked, so a hidden "{Range}_shown" marker says it was on the page.
    public static string Range(Guid resultItemId) => $"rg_{resultItemId:N}";

    public static string NilReturn(Guid participantId) => $"nr_{participantId:N}";
}
