namespace PTL.Core.Distribution;

// tblMonthlyDistributionScheme.fldEmailState - legacy ResultsTabulation.MonthlyDistributionScheme.EmailState.
public enum TabulationEmailState
{
    None = 0,
    FirstEmailSent = 1,
    ChaserEmailSent = 2,
}

// tblDistributionTabulation.fldPublishState - legacy SignOff.Tabulation.PublishStatus.
public enum TabulationPublishState
{
    AwaitingSignOff = 0,
    Authorized = 1,
    NotAuthorized = 2,
}

// spgMonthlyDistributionInfoForTabulationsById's third result set - legacy ResultsTabulation.TabulationInfo.
public sealed class TabulationEntity
{
    public Guid MonthlyDistributionSchemeId { get; set; }
    public Guid TabulationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PublishState { get; set; }

    // COUNT of tblPublishedTabulation rows - the number shown beside "Issued".
    public int PublishCount { get; set; }
}

// spgAssessmentStatus - legacy ResultsTabulation.AssessmentStatusInfo (one row per applicable sample receipt).
public sealed class TabulationAssessmentStatusEntity
{
    public DateTime? AssessmentSubmitted { get; set; }
    public DateTime? SubmissionDate { get; set; }
    public DateTime? NonSubmissionDate { get; set; }
    public DateTime? NonCapableDate { get; set; }
}

// spgMonthlyDistributionInfoForTabulationsById's second result set - legacy ResultsTabulation.MonthlyDistributionScheme.
public sealed class TabulationsSchemeEntity
{
    public Guid MonthlyDistributionSchemeId { get; set; }
    public string SchemeName { get; set; } = string.Empty;
    public string SchemeIdentifier { get; set; } = string.Empty;
    public string DistributionReference { get; set; } = string.Empty;
    public string DistributionReferenceSuffix { get; set; } = string.Empty;
    public string ScheduleCode { get; set; } = string.Empty;
    public DateTime ResultsIssueTargetDate { get; set; }
    public DateTime DeadlineDate { get; set; }
    public string? TestConsultant1Name { get; set; }
    public string? TestConsultant2Name { get; set; }
    public string? TestConsultant3Name { get; set; }
    public bool IsCommentSubmitted { get; set; }
    public Guid? TestConsultantTabulationId { get; set; }
    public int EmailState { get; set; }
    public DateTime? IsReadyForTestConsultant { get; set; }
    public DateTime? IsReadyForResultsSignOff { get; set; }
    public DateTime? IsReadyForAssessor { get; set; }
    public string? Assessor1Name { get; set; }
    public string? Assessor2Name { get; set; }
    public string? Assessor3Name { get; set; }
    public string? Assessor4Name { get; set; }
    public bool HasIntendedResults { get; set; }
    public bool RequiresAssessment { get; set; }

    public List<TabulationEntity> Tabulations { get; set; } = [];

    // Raw spgAssessmentStatus rows. Only read for schemes that require assessment - the three
    // values derived from them are consulted solely for those schemes.
    public List<TabulationAssessmentStatusEntity> AssessmentStatuses { get; set; } = [];

    // Legacy AssessmentStatusInfoCollection.CalculateIsAssessmentComplete: every applicable
    // distribution test has had its assessment submitted (vacuously true when there are no rows).
    public bool IsAssessmentComplete => AssessmentStatuses.All(s => IsSet(s.AssessmentSubmitted));

    // Legacy CalculateHasUnaccountedSamples: a sample receipt with no submission, non-submission or
    // non-capable date.
    public bool HasUnaccountedSamples =>
        AssessmentStatuses.Any(s => !IsSet(s.SubmissionDate) && !IsSet(s.NonSubmissionDate) && !IsSet(s.NonCapableDate));

    internal static bool IsSet(DateTime? date) => date is { } value && value != DateTime.MinValue;
}
