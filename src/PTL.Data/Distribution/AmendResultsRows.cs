namespace PTL.Data.Distribution;

// spgMonthlyDistributionSchemeForAmendResults result sets and spgaRating ("fld{Property}" convention).
internal static partial class TabulationReportRows
{
    public sealed class AmendHeader
    {
        public Guid MonthlyDistributionSchemeId { get; set; }
        public string? SchemeName { get; set; }
        public string? SchemeIdentifier { get; set; }
        public string? DistributionReference { get; set; }
        public string? DistributionReferenceSuffix { get; set; }
        public string? ScheduleCode { get; set; }
        public bool? StoreRatings { get; set; }
        public bool? RequiresAssessment { get; set; }
    }

    public sealed class AmendTest
    {
        public Guid TestId { get; set; }
        public string? TestType { get; set; }
        public DateTime? AssessmentSubmitted { get; set; }
    }

    public sealed class AmendMethodItem
    {
        public Guid TestId { get; set; }
        public Guid TestMethodItemId { get; set; }
        public string? TestMethodItemType { get; set; }
        public string? FormatString { get; set; }
        public string? Message { get; set; }
    }

    public sealed class AmendResultItem
    {
        public Guid TestId { get; set; }
        public Guid TestResultItemId { get; set; }
        public string? TestResultItemType { get; set; }
        public string? FormatString { get; set; }
        public string? Message { get; set; }
        public bool? IsRange { get; set; }
        public int? Order { get; set; }
        public Guid TestResultItemTypeId { get; set; }
    }

    public sealed class AmendSample
    {
        public Guid TestId { get; set; }
        public Guid SampleId { get; set; }
        public string? SampleNumber { get; set; }
    }

    public sealed class AmendParticipant
    {
        public Guid DistributionParticipantId { get; set; }
        public string? LabCode { get; set; }
        public string? NilReturnReason { get; set; }
    }

    public sealed class AmendSampleRating
    {
        public Guid SampleRatingId { get; set; }
        public Guid ParticipantId { get; set; }
        public Guid SampleId { get; set; }
        public Guid RatingId { get; set; }
    }

    public sealed class AmendParticipantMethod
    {
        public Guid ParticipantId { get; set; }
        public Guid TestMethodItemId { get; set; }
        public string? AmendedResult { get; set; }
    }

    public sealed class AmendParticipantResult
    {
        public Guid SampleId { get; set; }
        public Guid ParticipantId { get; set; }
        public Guid TestResultItemId { get; set; }
        public string? AmendedResult { get; set; }
    }

    public sealed class AmendAssessment
    {
        public Guid SampleReceiptId { get; set; }
        public string? AssessmentComment { get; set; }
        public int? Score { get; set; }
        public Guid ParticipantId { get; set; }
        public Guid SampleId { get; set; }
        public Guid TestId { get; set; }
    }

    public sealed class AmendParticipantCategory
    {
        public Guid ParticipantId { get; set; }
        public Guid CategoryItemId { get; set; }
        public int? AmendedCategoryScore { get; set; }
        public Guid SampleId { get; set; }
        public Guid SampleTestId { get; set; }
    }

    public sealed class AmendIntendedMethod
    {
        public Guid TestMethodItemId { get; set; }
        public string? IntendedResult { get; set; }
    }

    public sealed class AmendIntendedResult
    {
        public Guid SampleId { get; set; }
        public Guid TestResultItemId { get; set; }
        public string? IntendedResult { get; set; }
        public string? IntendedResult2 { get; set; }
    }

    public sealed class RatingRow
    {
        public Guid RatingId { get; set; }
        public string? Rating { get; set; }
    }
}
