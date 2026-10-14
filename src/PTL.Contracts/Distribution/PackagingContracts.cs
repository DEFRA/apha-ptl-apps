namespace PTL.Contracts.Distribution;

// Sample Packaging screen - legacy Internal User/PackagingSheet.aspx. Mode is the dropdown value
// (0 Group Address Packaging, 1 Participant Combined Packaging, 2 Daily Packaging); Week (1-4)
// applies to modes 0 and 1 only.
public sealed record PackagingParticipantResponse(
    Guid DistributionParticipantId,
    string Description,
    DateTime? PackagedDate,
    DateTime? PostedDate,
    string ImportPermitStatus);

// PackedTotal/PostedTotal: 0 none, 1 complete, 2 selectable ("All" checkbox).
public sealed record PackagingSchemeResponse(
    Guid MonthlyDistributionSchemeId,
    string Description,
    bool PreparationComplete,
    string PackingInfo,
    string CustomerPackingInfo,
    string PSLevelPackingInfo,
    string ImportPermitStatus,
    Guid? DistributionParticipantId,
    DateTime? PackagedDate,
    DateTime? PostedDate,
    int PackedTotal,
    int PostedTotal,
    IReadOnlyList<PackagingParticipantResponse> Participants);

public sealed record PackagingBlockResponse(
    string PrintKey,
    DateTime Date,
    string Heading,
    string PackingInfo,
    bool DueToBePosted,
    bool ShowAllPacked,
    bool ShowAllPosted,
    IReadOnlyList<PackagingSchemeResponse> Schemes);

public sealed record PackagingSheetResponse(
    int Mode,
    int Week,
    int YearId,
    int MonthId,
    IReadOnlyList<PackagingBlockResponse> Blocks);

public sealed record PackagingStageUpdateRequest(int Mode, int Week, Guid DistributionParticipantId, bool Packed, bool Posted);

public sealed record PackagingSaveRequest(IReadOnlyList<PackagingStageUpdateRequest> Updates);

public sealed record PackagingSaveResult(bool Success);

// The rows ticked in the Print column (PackagingBlockResponse.PrintKey).
public sealed record PackagingSelectionRequest(int Mode, int Week, IReadOnlyList<string> SelectedKeys);

public sealed record PackagingLabelResponse(IReadOnlyList<string> Lines);

public sealed record PackagingLabelsResponse(IReadOnlyList<PackagingLabelResponse> Labels);

// Print Customs outcome as seen by InternalWeb: the Word document, or the reason it could not be built.
public sealed record PackagingCustomsResult(byte[]? Content, string FileName, string? Error);
