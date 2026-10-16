namespace PTL.Core.Distribution;

// Sample Receipt screen (legacy Internal User/SampleReceipt.aspx, sitemap title "Submission Status").
// IsReadOnly = the legacy "controls disabled" state: no Tests, or the scheme is already ready for
// the assessor. SchemeHeading/LabHeading are the two legacy h3 lines.
public sealed record SampleReceiptPage(
    string SchemeHeading,
    string LabHeading,
    bool IsReadOnly,
    IReadOnlyList<SampleReceiptTest> Tests);

public sealed record SampleReceiptTest(Guid TestId, string TestType, IReadOnlyList<SampleReceiptSample> Samples);

public sealed record SampleReceiptSample(Guid SampleReceiptId, string SampleNumber, bool Submitted, bool NotSubmitted, bool NotInRepertoire);

// The posted tick boxes for one Sample.
public sealed record SampleReceiptUpdate(Guid SampleReceiptId, bool Submitted, bool NotSubmitted, bool NotInRepertoire);

public enum SampleReceiptSaveStatus
{
    Saved,
    NotFound,
    ReadOnly,
}

public sealed record SampleReceiptSaveOutcome(SampleReceiptSaveStatus Status)
{
    public bool Success => Status == SampleReceiptSaveStatus.Saved;
}
