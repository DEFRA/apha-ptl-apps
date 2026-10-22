namespace PTL.Data.Distribution;

// spgMonthlyDistributionInfoForTestConsultant and the parts of spgMonthlyDistributionSchemeForTestConsultant that
// the draft tabulation rows (DraftTest, DraftMethodItem, ... in TabulationReportRows) do not already cover
// ("fld{Property}" convention).
internal static partial class TabulationReportRows
{
    public sealed class TestConsultantMonth
    {
        public Guid MonthlyDistributionId { get; set; }
        public int YearId { get; set; }
        public int MonthId { get; set; }
    }

    public sealed class TestConsultantScheme
    {
        public int YearId { get; set; }
        public Guid MonthlyDistributionId { get; set; }
        public Guid MonthlyDistributionSchemeId { get; set; }
        public string? SchemeName { get; set; }
        public string? SchemeIdentifier { get; set; }
        public string? DistributionReference { get; set; }
        public string? DistributionReferenceSuffix { get; set; }
        public string? ScheduleCode { get; set; }
        public DateTime? ResultsIssueTargetDate { get; set; }
        public bool? IsCommentSubmitted { get; set; }
    }

    public sealed class TestConsultantHeader
    {
        public Guid MonthlyDistributionSchemeId { get; set; }
        public string? SchemeName { get; set; }
        public string? SchemeIdentifier { get; set; }
        public string? DistributionReference { get; set; }
        public string? DistributionReferenceSuffix { get; set; }
        public DateTime? DistributionDate { get; set; }
        public string? ScheduleCode { get; set; }
        public string? TestConsultantComments { get; set; }
        public bool? IsCommentSubmitted { get; set; }
        public string? TestConsultantCommentsAuthor { get; set; }
        public string? Comments { get; set; }
        public bool? ShowRatings { get; set; }
        public string? StandardTabulationText { get; set; }
    }

    public sealed class TestConsultantParticipant
    {
        public Guid ParticipantId { get; set; }
        public string? LabCode { get; set; }
        public string? DeadlineChangeReason { get; set; }
        public bool? ExtraDocumentsReturned { get; set; }
        public string? NilReturnReason { get; set; }
    }

    public sealed class TestConsultantSampleDetail
    {
        public string? SampleNumber { get; set; }
        public string? TabulationDescription { get; set; }
        public string? TestConsultantsDescription { get; set; }
    }

    public sealed class TestConsultantSchemeState
    {
        public Guid? TestConsultant1Id { get; set; }
        public Guid? TestConsultant2Id { get; set; }
        public Guid? TestConsultant3Id { get; set; }
        public bool? IsCommentSubmitted { get; set; }
        public string? TestConsultantComments { get; set; }
        public Guid? TestConsultantCommentsAuthorId { get; set; }
    }
}
