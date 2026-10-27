namespace PTL.Data.Distribution;

// Result-set rows for spgDistributionTabulation, spgPublishedTabulation, spgPublishedTabulations and
// the two assessment score procedures. Every property is the procedure's column name without the
// "fld" prefix, which is how DapperColumnMappings maps them.
internal static partial class TabulationReportRows
{
    public sealed class DraftHeader
    {
        public Guid DistributionTabulationId { get; set; }
        public Guid MonthlyDistributionSchemeId { get; set; }
        public string? TabulationName { get; set; }
        public string? SchemeName { get; set; }
        public string? SchemeIdentifier { get; set; }
        public bool? RequiresAssessment { get; set; }
        public string? DistributionReference { get; set; }
        public string? DistributionReferenceSuffix { get; set; }
        public string? ScheduleCode { get; set; }
        public string? Comments { get; set; }
        public string? TestConsultantComments { get; set; }
        public string? TestConsultantCommentsAuthor { get; set; }
        public string? TestConsultantCommentsDept { get; set; }
        public DateTime? DistributionDate { get; set; }
        public bool? ShowUkasLogo { get; set; }
        public DateTime? SchemeVersionDate { get; set; }
        public bool? HasRatings { get; set; }
        public bool? IntendedResultsOnly { get; set; }
        public bool? SingleParticipantTabulation { get; set; }
        public bool? AvailableToParticipants { get; set; }
        public bool? AvailableToViewers { get; set; }
        public string? StandardTabulationText { get; set; }
    }

    public sealed class DraftTest
    {
        public Guid TestId { get; set; }
        public string? TestType { get; set; }
        public DateTime? AssessmentSubmitted { get; set; }
        public Guid? AssessorId { get; set; }
        public string? AssessorName { get; set; }
    }

    public sealed class DraftMethodItem
    {
        public Guid TestId { get; set; }
        public Guid TestMethodItemId { get; set; }
        public string? TestMethodItemType { get; set; }
    }

    public sealed class DraftResultItem
    {
        public Guid TestId { get; set; }
        public Guid TestResultItemId { get; set; }
        public string? TestResultItemType { get; set; }
    }

    public sealed class DraftCategoryItem
    {
        public Guid CategoryItemId { get; set; }
        public Guid TestId { get; set; }
        public string? Name { get; set; }
    }

    public sealed class DraftSample
    {
        public Guid TestId { get; set; }
        public Guid SampleId { get; set; }
        public string? SampleNumber { get; set; }
        public string? TabulationDescription { get; set; }
        public Guid SampleTestId { get; set; }
    }

    public sealed class DraftParticipant
    {
        public Guid DistributionParticipantId { get; set; }
        public Guid ParticipantId { get; set; }
        public string? LabCode { get; set; }
        public string? NilReturnReason { get; set; }
    }

    public sealed class DraftRating
    {
        public Guid SampleId { get; set; }
        public Guid ParticipantId { get; set; }
        public string? Rating { get; set; }
    }

    public sealed class DraftParticipantMethod
    {
        public Guid ParticipantId { get; set; }
        public Guid TestMethodItemId { get; set; }
        public string? AmendedResult { get; set; }
    }

    public sealed class DraftParticipantResult
    {
        public Guid SampleId { get; set; }
        public Guid ParticipantId { get; set; }
        public Guid TestResultItemId { get; set; }
        public string? AmendedResult { get; set; }
    }

    public sealed class DraftParticipantCategory
    {
        public Guid ParticipantId { get; set; }
        public Guid CategoryItemId { get; set; }
        public Guid SampleTestId { get; set; }
        public int? CategoryScore { get; set; }
        public Guid SampleId { get; set; }
        public Guid TestId { get; set; }
        public DateTime? AssessmentSubmitted { get; set; }
    }

    public sealed class DraftParticipantCriterion
    {
        public Guid ParticipantId { get; set; }
        public Guid CategoryItemId { get; set; }
        public Guid TestId { get; set; }
        public Guid CriterionItemId { get; set; }
        public Guid SampleId { get; set; }
        public string? Name { get; set; }
    }

    public sealed class DraftIntendedMethod
    {
        public Guid TestMethodItemId { get; set; }
        public string? IntendedResult { get; set; }
    }

    public sealed class DraftIntendedResult
    {
        public Guid SampleId { get; set; }
        public Guid TestResultItemId { get; set; }
        public string? IntendedResult { get; set; }
        public string? IntendedResult2 { get; set; }
    }

    public sealed class Viewer
    {
        public Guid ViewerId { get; set; }
        public string? Name { get; set; }
    }

    public sealed class ViewerParticipant
    {
        public Guid ViewerId { get; set; }
        public Guid ParticipantId { get; set; }
    }

    public sealed class DraftAssessment
    {
        public Guid TestId { get; set; }
        public Guid SampleId { get; set; }
        public Guid SampleReceiptId { get; set; }
        public Guid SampleTestId { get; set; }
        public Guid ParticipantId { get; set; }
        public int? Score { get; set; }
        public string? AssessmentComment { get; set; }
        public Guid? AssessorId { get; set; }
        public string? AssessorName { get; set; }
        public DateTime? AssessmentDate { get; set; }
        public DateTime? SubmissionDate { get; set; }
        public DateTime? NonSubmissionDate { get; set; }
        public DateTime? NonCapableDate { get; set; }
        public DateTime? AssessmentSubmitted { get; set; }
    }

    public sealed class DraftAttachment
    {
        public Guid MonthlyDistributionAttachmentId { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public byte[]? FileData { get; set; }
        public DateTime? UploadDate { get; set; }
    }

    public sealed class PublishedHeader
    {
        public Guid PublishedTabulationId { get; set; }
        public string? Name { get; set; }
        public string? Signatory { get; set; }
        public DateTime? CreationDate { get; set; }
        public string? Distribution { get; set; }
        public DateTime? DistributionDate { get; set; }
        public string? SchemeIdentifier { get; set; }
        public string? SchemeName { get; set; }
        public bool? RequiresAssessment { get; set; }
        public Guid DistributionTabulationId { get; set; }
        public string? SampleComments { get; set; }
        public string? TestConsultantComments { get; set; }
        public string? TestConsultantCommentsAuthor { get; set; }
        public string? TestConsultantCommentsDept { get; set; }
        public bool? IsAccredited { get; set; }
        public DateTime? SchemeVersionDate { get; set; }
        public bool? HasRatings { get; set; }
        public bool? SingleParticipantTabulation { get; set; }
        public bool? AvailableToViewers { get; set; }
        public bool? IntendedResultsOnly { get; set; }
        public string? StandardTabulationText { get; set; }
    }

    public sealed class PublishedTest
    {
        public Guid PublishedTestId { get; set; }
        public string? Name { get; set; }
    }

    public sealed class PublishedSample
    {
        public Guid PublishedSampleId { get; set; }
        public Guid PublishedTestId { get; set; }
        public string? SampleNumber { get; set; }
        public string? TabulationDescription { get; set; }
    }

    public sealed class PublishedMethodItem
    {
        public Guid PublishedTestMethodItemId { get; set; }
        public Guid PublishedTestId { get; set; }
        public string? Name { get; set; }
    }

    public sealed class PublishedResultItem
    {
        public Guid PublishedTestResultItemId { get; set; }
        public Guid PublishedTestId { get; set; }
        public string? Name { get; set; }
    }

    public sealed class PublishedCategoryItem
    {
        public Guid PublishedCategoryItemId { get; set; }
        public Guid PublishedTestId { get; set; }
        public string? Name { get; set; }
    }

    public sealed class PublishedParticipant
    {
        public Guid PublishedParticipantId { get; set; }
        public string? LabCode { get; set; }
        public string? NilReturnReason { get; set; }
    }

    public sealed class PublishedRating
    {
        public Guid PublishedParticipantId { get; set; }
        public Guid PublishedSampleId { get; set; }
        public string? Rating { get; set; }
    }

    public sealed class PublishedParticipantMethod
    {
        public Guid PublishedParticipantId { get; set; }
        public Guid PublishedTestMethodItemId { get; set; }
        public string? Result { get; set; }
    }

    public sealed class PublishedParticipantResult
    {
        public Guid PublishedParticipantId { get; set; }
        public Guid PublishedTestResultItemId { get; set; }
        public Guid PublishedSampleId { get; set; }
        public string? Result { get; set; }
    }

    public sealed class PublishedParticipantCategory
    {
        public Guid PublishedParticipantId { get; set; }
        public Guid PublishedCategoryItemId { get; set; }
        public int? Score { get; set; }
        public Guid PublishedSampleId { get; set; }
    }

    public sealed class PublishedParticipantCriterion
    {
        public Guid PublishedParticipantId { get; set; }
        public Guid PublishedSampleId { get; set; }
        public Guid PublishedCategoryItemId { get; set; }
        public string? Name { get; set; }
    }

    public sealed class PublishedIntendedMethod
    {
        public Guid PublishedTestMethodItemId { get; set; }
        public string? Result { get; set; }
    }

    public sealed class PublishedIntendedResult
    {
        public Guid PublishedTestResultItemId { get; set; }
        public Guid PublishedSampleId { get; set; }
        public string? Result { get; set; }
        public string? Result2 { get; set; }
    }

    public sealed class PublishedAssessment
    {
        public Guid PublishedTestId { get; set; }
        public Guid PublishedSampleId { get; set; }
        public Guid PublishedParticipantId { get; set; }
        public int? AssessmentScore { get; set; }
        public string? AssessmentComment { get; set; }
        public DateTime? SubmissionDate { get; set; }
        public DateTime? NonSubmissionDate { get; set; }
        public DateTime? NonCapableDate { get; set; }
    }

    public sealed class PublishedAttachment
    {
        public Guid PublishedAttachmentId { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public byte[]? FileData { get; set; }
    }

    public sealed class PublishedTabulationsHeader
    {
        public string? TabulationName { get; set; }
        public string? Distribution { get; set; }
        public string? SchemeIdentifier { get; set; }
        public string? SchemeName { get; set; }
    }

    public sealed class PublishedTabulationsRow
    {
        public Guid PublishedTabulationId { get; set; }
        public string? Signatory { get; set; }
        public DateTime CreationDate { get; set; }
    }

    public sealed class AssessmentScoreHeader
    {
        public string? TabulationName { get; set; }
        public string? SchemeName { get; set; }
        public string? Distribution { get; set; }
        public string? TestName { get; set; }
        public DateTime? AssessmentSubmitted { get; set; }
    }

    public sealed class AssessmentScoreReceiptRow
    {
        public DateTime? SubmissionDate { get; set; }
        public DateTime? NonSubmissionDate { get; set; }
        public DateTime? NonCapableDate { get; set; }
        public int? Score { get; set; }
        public DateTime? AssessmentDate { get; set; }
    }

    public sealed class CommentsHeader
    {
        public Guid MonthlyDistributionSchemeId { get; set; }
        public string? SchemeName { get; set; }
        public string? SchemeIdentifier { get; set; }
        public string? DistributionReference { get; set; }
        public string? DistributionReferenceSuffix { get; set; }
        public string? ScheduleCode { get; set; }
        public string? TestConsultantComments { get; set; }
        public bool? IsCommentSubmitted { get; set; }
        public Guid? TestConsultantCommentsAuthorId { get; set; }
        public string? TestConsultantCommentsAuthor { get; set; }
        public string? TestConsultantCommentsDept { get; set; }
        public bool? RequiresAssessment { get; set; }
    }
}
