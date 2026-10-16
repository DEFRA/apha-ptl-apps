namespace PTL.Contracts.Distribution;

// Results Receipt screen (legacy Internal User/ResultsReceipt.aspx).
public sealed record ResultsReceiptResponse(
    Guid MonthlyDistributionId,
    int YearId,
    int MonthId,
    IReadOnlyList<ResultsReceiptSchemeResponse> Schemes);

public sealed record ResultsReceiptSchemeResponse(
    Guid MonthlyDistributionSchemeId,
    string Description,
    bool RequiresAssessment,
    int ProgressPercentage,
    IReadOnlyList<ResultsReceiptParticipantResponse> Participants);

// FirstEditDate null = legacy "No Results". CanViewResults/CanManageSamples false = the legacy
// View/Edit links are disabled (Samples not yet posted or no Sample Numbers defined).
public sealed record ResultsReceiptParticipantResponse(
    Guid DistributionParticipantId,
    string FullName,
    DateTime DeadlineDate,
    DateTime? FirstEditDate,
    DateTime? LastEditDate,
    bool ExtraDocumentsReturned,
    bool CanViewResults,
    bool HasUnaccountedSamples,
    DateTime? ReturnedDate);

public sealed record ResultsReceiptSaveRequest(IReadOnlyList<ResultsReceiptParticipantUpdateRequest> Updates);

public sealed record ResultsReceiptParticipantUpdateRequest(
    Guid DistributionParticipantId,
    bool ExtraDocumentsReturned,
    bool ReturnedChecked);

public sealed record ResultsReceiptSaveResult(bool Success);

// Sample Receipt screen (legacy Internal User/SampleReceipt.aspx, sitemap title "Submission Status").
// IsReadOnly = no Tests, or the scheme is already ready for the assessor.
public sealed record SampleReceiptResponse(
    string SchemeHeading,
    string LabHeading,
    bool IsReadOnly,
    IReadOnlyList<SampleReceiptTestResponse> Tests);

public sealed record SampleReceiptTestResponse(Guid TestId, string TestType, IReadOnlyList<SampleReceiptSampleResponse> Samples);

public sealed record SampleReceiptSampleResponse(Guid SampleReceiptId, string SampleNumber, bool Submitted, bool NotSubmitted, bool NotInRepertoire);

public sealed record SampleReceiptSaveRequest(IReadOnlyList<SampleReceiptSampleUpdateRequest> Samples);

public sealed record SampleReceiptSampleUpdateRequest(Guid SampleReceiptId, bool Submitted, bool NotSubmitted, bool NotInRepertoire);

// ReadOnly mirrors legacy's disabled Save/Apply: nothing is written once results are ready for the assessor.
public sealed record SampleReceiptSaveResult(bool Success, bool ReadOnly = false);
