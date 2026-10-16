namespace PTL.Core.Distribution;

// spgSampleReceipt's first result set - legacy ResultsReceipt.Test (only Tests with an applicable
// Sample and a Sample Receipt row for this participant are returned).
public sealed class SampleReceiptTestEntity
{
    public Guid TestId { get; set; }
    public string TestType { get; set; } = string.Empty;
}

// spgSampleReceipt's second result set - legacy ResultsReceipt.ParticipantSampleReceipt. The three
// dates are mutually exclusive in practice: at most one of them is set per Sample.
public sealed class SampleReceiptSampleEntity
{
    public Guid SampleReceiptId { get; set; }
    public Guid TestId { get; set; }
    public string SampleNumber { get; set; } = string.Empty;
    public DateTime? SubmissionDate { get; set; }
    public DateTime? NonSubmissionDate { get; set; }
    public DateTime? NonCapableDate { get; set; }
}

// spgSampleReceipt's third result set.
public sealed class SampleReceiptLabEntity
{
    public string LabCode { get; set; } = string.Empty;
    public string LabName { get; set; } = string.Empty;
}

// spgSampleReceipt's fourth result set - legacy ResultsReceipt.TestCollection's scheme columns.
public sealed class SampleReceiptSchemeEntity
{
    public string DistributionReference { get; set; } = string.Empty;
    public string DistributionReferenceSuffix { get; set; } = string.Empty;
    public string ScheduleCode { get; set; } = string.Empty;
    public DateTime? IsReadyForAssessor { get; set; }
    public string SchemeName { get; set; } = string.Empty;
    public string SchemeIdentifier { get; set; } = string.Empty;

    public string DistributionReferenceFull => $"{DistributionReference}{DistributionReferenceSuffix}/{ScheduleCode}";
}

public sealed record SampleReceiptData(
    SampleReceiptSchemeEntity Scheme,
    SampleReceiptLabEntity Lab,
    IReadOnlyList<SampleReceiptTestEntity> Tests,
    IReadOnlyList<SampleReceiptSampleEntity> Samples);
