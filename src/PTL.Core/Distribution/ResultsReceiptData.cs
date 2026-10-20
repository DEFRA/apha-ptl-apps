namespace PTL.Core.Distribution;

// spgMonthlyDistributionForResultsReceipt's first result set - legacy
// PtaBusinessObjects.BusinessObjects.Distributions.ResultsReceipt.MonthlyDistribution.
public sealed class ResultsReceiptHeaderEntity
{
    public Guid MonthlyDistributionId { get; set; }
    public int YearId { get; set; }
    public int MonthId { get; set; }
}

// spgMonthlyDistributionForResultsReceipt's second result set - legacy ResultsReceipt.MonthlyDistributionScheme.
public sealed class ResultsReceiptSchemeEntity
{
    public Guid MonthlyDistributionSchemeId { get; set; }
    public string SchemeIdentifier { get; set; } = string.Empty;
    public string SchemeName { get; set; } = string.Empty;
    public string DistributionReference { get; set; } = string.Empty;
    public string DistributionReferenceSuffix { get; set; } = string.Empty;
    public string ScheduleCode { get; set; } = string.Empty;
    public bool HasSampleNumbersDefined { get; set; }
    public DateTime DeadlineDate { get; set; }
    public bool RequiresAssessment { get; set; }
    public List<ResultsReceiptParticipantEntity> Participants { get; set; } = [];

    public string DistributionReferenceFull => $"{DistributionReference}{DistributionReferenceSuffix}/{ScheduleCode}";

    // Legacy "Reference/Code: Identifier Name" scheme row text.
    public string Description => $"{DistributionReferenceFull}: {SchemeIdentifier} {SchemeName}";

    // Legacy progress bar: share of participants with a First Edit date, via CType(Double, Integer),
    // which rounds to even rather than truncating.
    public int ProgressPercentage =>
        Participants.Count == 0
            ? 0
            : (int)Math.Round((double)Participants.Count(p => p.FirstEditDate is not null) / Participants.Count * 100, MidpointRounding.ToEven);

    // Legacy: the View/Edit links are disabled until Sample Numbers are defined and the participant's samples are posted.
    public bool CanViewResults(ResultsReceiptParticipantEntity participant) =>
        HasSampleNumbersDefined && participant.PostedDate is not null;
}

// spgMonthlyDistributionForResultsReceipt's third result set - legacy ResultsReceipt.DistributionParticipant.
public sealed class ResultsReceiptParticipantEntity
{
    public Guid DistributionParticipantId { get; set; }
    public Guid MonthlyDistributionSchemeId { get; set; }
    public string LabCode { get; set; } = string.Empty;
    public string LabName { get; set; } = string.Empty;
    public bool ExtraDocumentsReturned { get; set; }
    public DateTime? FirstEditDate { get; set; }
    public DateTime? LastEditDate { get; set; }
    public DateTime? DeadlineDate { get; set; }
    public string? DeadlineChangeReason { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? ReturnedDate { get; set; }
    public bool HasUnaccountedSamples { get; set; }

    public string FullName => $"{LabCode}: {LabName}";
}

// spgMonthlyDistributionForResultsReceipt's fourth result set (schemes that require assessment only).
public sealed class ResultsReceiptSampleStatusEntity
{
    public Guid DistributionParticipantId { get; set; }
    public bool HasUnaccountedSamples { get; set; }
}

public sealed record ResultsReceiptData(
    Guid MonthlyDistributionId,
    int YearId,
    int MonthId,
    IReadOnlyList<ResultsReceiptSchemeEntity> Schemes);
