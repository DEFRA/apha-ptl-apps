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
