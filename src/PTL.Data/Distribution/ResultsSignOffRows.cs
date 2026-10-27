namespace PTL.Data.Distribution;

// spgaMonthlyDistributionInfoForResultsSignOff (months, schemes, tabulations) and the allocated names read next to it
// ("fld{Property}" convention, like the other rows here).
internal static partial class TabulationReportRows
{
    public sealed class ResultsSignOffMonth
    {
        public Guid MonthlyDistributionId { get; set; }
        public int YearId { get; set; }
        public int MonthId { get; set; }
    }

    public sealed class ResultsSignOffScheme
    {
        public Guid MonthlyDistributionId { get; set; }
        public Guid MonthlyDistributionSchemeId { get; set; }
        public string? SchemeName { get; set; }
        public string? SchemeIdentifier { get; set; }
        public string? DistributionReference { get; set; }
        public string? DistributionReferenceSuffix { get; set; }
        public string? ScheduleCode { get; set; }
        public DateTime? ResultsIssueTargetDate { get; set; }
    }

    public sealed class ResultsSignOffTabulation
    {
        public Guid MonthlyDistributionId { get; set; }
        public Guid MonthlyDistributionSchemeId { get; set; }
        public Guid DistributionTabulationId { get; set; }
        public string? Name { get; set; }
        public int? PublishCount { get; set; }
        public bool? SingleParticipantTabulation { get; set; }
        public bool? AvailableToParticipants { get; set; }
        public bool? AvailableToViewers { get; set; }
    }

    public sealed class ResultsSignOffNames
    {
        public Guid MonthlyDistributionSchemeId { get; set; }
        public string? TestConsultant1 { get; set; }
        public string? TestConsultant2 { get; set; }
        public string? TestConsultant3 { get; set; }
        public string? Assessor1 { get; set; }
        public string? Assessor2 { get; set; }
        public string? Assessor3 { get; set; }
        public string? Assessor4 { get; set; }
    }

    // The rest are read when a tabulation is authorised (spgDistributionTabulation's criterion items, the scheme's month,
    // year and accreditation, the distribution's samples, the signatory and the notification recipients).
    public sealed class DraftCriterionItem
    {
        public Guid TestId { get; set; }
        public Guid CategoryItemId { get; set; }
        public Guid CriterionItemId { get; set; }
        public string? Name { get; set; }
    }

    public sealed class PublishScheme
    {
        public int? MonthId { get; set; }
        public int? YearId { get; set; }
        public bool? IsAccredited { get; set; }
        public bool? RequiresAssessment { get; set; }
    }

    public sealed class PublishSample
    {
        public Guid SampleId { get; set; }
        public string? SampleNumber { get; set; }
        public string? TabulationDescription { get; set; }
    }

    public sealed class PublishSignatory
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
    }

    public sealed class PublishRecipient
    {
        public string? Email { get; set; }
        public string? Email2 { get; set; }
        public string? Name { get; set; }
    }
}
