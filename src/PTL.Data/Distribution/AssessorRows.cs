namespace PTL.Data.Distribution;

// The Assessor pages' stored procedures (spgYearByAssessorId, spgMonthlyDistributionInfoForAssessor,
// spgAssessmentItemByTestId), read with the "fld{Property}" convention like the other rows here.
internal static partial class TabulationReportRows
{
    public sealed class AssessorYear
    {
        public int YearId { get; set; }
    }

    public sealed class AssessorMonth
    {
        public Guid MonthlyDistributionId { get; set; }
        public int YearId { get; set; }
        public int MonthId { get; set; }
    }

    public sealed class AssessorDistribution
    {
        public Guid MonthlyDistributionId { get; set; }
        public Guid MonthlyDistributionSchemeId { get; set; }
        public string? SchemeName { get; set; }
        public string? SchemeIdentifier { get; set; }
        public string? DistributionReference { get; set; }
        public string? DistributionReferenceSuffix { get; set; }
        public string? ScheduleCode { get; set; }
    }

    public sealed class AssessorTest
    {
        public Guid TestId { get; set; }
        public string? TestType { get; set; }
        public Guid MonthlyDistributionSchemeId { get; set; }
        public DateTime? AssessmentSubmitted { get; set; }
    }

    public sealed class AssessorItemHeader
    {
        public DateTime? AssessmentSubmitted { get; set; }
        public string? AssessorName { get; set; }
        public string? SchemeName { get; set; }
        public string? SchemeIdentifier { get; set; }
        public string? DistributionReference { get; set; }
        public string? DistributionReferenceSuffix { get; set; }
        public string? ScheduleCode { get; set; }
        public string? TestName { get; set; }
        public int YearId { get; set; }
        public int MonthId { get; set; }
    }

    public sealed class AssessorItemSample
    {
        public Guid SampleId { get; set; }
        public string? SampleNumber { get; set; }
    }

    public sealed class AssessorItem
    {
        public Guid SampleId { get; set; }
        public Guid SampleTestId { get; set; }
        public Guid DistributionParticipantId { get; set; }
        public string? Labcode { get; set; }
        public DateTime? AssessmentDate { get; set; }
    }

    // spgAssessmentEntry: the submission, the test's categories, the saved category scores, the criteria and the ticked criteria.
    public sealed class AssessorEntryHeader
    {
        public Guid SampleReceiptId { get; set; }
        public string? AssessmentComment { get; set; }
        public Guid? AssessorId { get; set; }
        public string? AssessorName { get; set; }
        public int? Score { get; set; }
        public string? SchemeName { get; set; }
        public string? SchemeIdentifier { get; set; }
        public string? DistributionReference { get; set; }
        public string? DistributionReferenceSuffix { get; set; }
        public string? ScheduleCode { get; set; }
        public string? TestName { get; set; }
        public int YearId { get; set; }
        public int MonthId { get; set; }
        public string? Labcode { get; set; }
        public string? SampleNumber { get; set; }
        public Guid TestId { get; set; }
        public DateTime? AssessmentSubmitted { get; set; }
    }

    public sealed class AssessorEntryCategory
    {
        public Guid CategoryItemId { get; set; }
        public string? Name { get; set; }
    }

    public sealed class AssessorEntryActualCategory
    {
        public Guid CategoryItemId { get; set; }
        public int? CategoryScore { get; set; }
    }

    public sealed class AssessorEntryCriterion
    {
        public Guid CriterionItemId { get; set; }
        public string? Name { get; set; }
        public Guid CategoryItemId { get; set; }
    }

    public sealed class AssessorEntryActualCriterion
    {
        public Guid CategoryItemId { get; set; }
        public Guid CriterionItemId { get; set; }
    }

    public sealed class AssessorResultItem
    {
        public string? TestResultItemName { get; set; }
        public string? Result { get; set; }
    }

    // The assignment check: TestId is the test the submission belongs to, IsAssessor 1 when the user is one of its scheme's Assessors.
    public sealed class AssessorEntryAccess
    {
        public int IsAssessor { get; set; }
        public Guid TestId { get; set; }
    }
}
