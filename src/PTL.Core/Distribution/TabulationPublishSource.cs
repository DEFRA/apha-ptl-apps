namespace PTL.Core.Distribution;

// Everything legacy Published/PublishTabulation.vb read before it copied a draft tabulation into the published tables:
// ResultsTabulation.Tabulation (spgDistributionTabulation), the scheme's month, year and accreditation
// (spgMonthlyDistributionScheme) and every sample of the distribution (spgIntendedResults). Like legacy's data layer, a
// missing value is already its default here (empty string or GUID), except dates, which stay null.
public sealed record TabulationPublishSource(
    PublishHeader Header,
    PublishScheme Scheme,
    IReadOnlyList<PublishSample> Samples,
    IReadOnlyList<PublishTest> Tests,
    IReadOnlyList<PublishParticipant> Participants,
    IReadOnlyList<PublishIntendedMethod> IntendedMethods,
    IReadOnlyList<PublishIntendedResult> IntendedResults,
    IReadOnlyList<PublishAttachment> Attachments);

public sealed record PublishHeader(
    Guid DistributionTabulationId,
    Guid MonthlyDistributionSchemeId,
    string TabulationName,
    string SchemeName,
    string SchemeIdentifier,
    bool RequiresAssessment,
    string DistributionReference,
    string DistributionReferenceSuffix,
    string ScheduleCode,
    string Comments,
    string TestConsultantComments,
    string TestConsultantCommentsAuthor,
    string TestConsultantCommentsDept,
    DateTime DistributionDate,
    DateTime SchemeVersionDate,
    bool HasRatings,
    bool IntendedResultsOnly,
    bool SingleParticipantTabulation,
    bool AvailableToParticipants,
    bool AvailableToViewers,
    string StandardTabulationText);

public sealed record PublishScheme(int MonthId, int YearId, bool IsAccredited, bool RequiresAssessment);

public sealed record PublishSample(Guid SampleId, string SampleNumber, string TabulationDescription);

public sealed record PublishNamedItem(Guid Id, string Name);

public sealed record PublishCategoryItem(Guid CategoryItemId, string Name, IReadOnlyList<PublishNamedItem> Criteria);

public sealed record PublishTestSample(Guid SampleId, Guid SampleTestId);

public sealed record PublishTest(
    Guid TestId,
    string Name,
    DateTime? AssessmentSubmitted,
    Guid AssessorId,
    string AssessorName,
    IReadOnlyList<PublishTestSample> Samples,
    IReadOnlyList<PublishNamedItem> ResultItems,
    IReadOnlyList<PublishNamedItem> MethodItems,
    IReadOnlyList<PublishCategoryItem> CategoryItems);

public sealed record PublishRating(Guid SampleId, string Rating);

public sealed record PublishMethodResult(Guid TestMethodItemId, string Result);

public sealed record PublishResultItemResult(Guid TestResultItemId, Guid SampleId, string Result);

public sealed record PublishAssessment(
    Guid SampleTestId,
    Guid SampleReceiptId,
    string Comment,
    Guid AssessorId,
    string AssessorName,
    DateTime? AssessmentDate,
    int Score,
    DateTime? SubmissionDate,
    DateTime? NonSubmissionDate,
    DateTime? NonCapableDate);

public sealed record PublishCategoryResult(Guid CategoryItemId, Guid SampleId, int Score, IReadOnlyList<Guid> CriterionItemIds);

public sealed record PublishParticipant(
    Guid DistributionParticipantId,
    string LabCode,
    string NilReturnReason,
    IReadOnlyList<PublishRating> Ratings,
    IReadOnlyList<PublishMethodResult> MethodResults,
    IReadOnlyList<PublishResultItemResult> ResultItemResults,
    IReadOnlyList<PublishAssessment> Assessments,
    IReadOnlyList<PublishCategoryResult> CategoryResults);

public sealed record PublishIntendedMethod(Guid TestMethodItemId, string Result);

public sealed record PublishIntendedResult(Guid TestResultItemId, Guid SampleId, string Result, string Result2);

public sealed record PublishAttachment(Guid AttachmentId, string FileName, string ContentType, byte[] FileData, DateTime? UploadDate);
