namespace PTL.Contracts.Distribution;

// Sample Preparation screen (legacy Internal User/PreparationSheet.aspx).
public sealed record PreparationSheetResponse(
    Guid MonthlyDistributionId,
    int YearId,
    int MonthId,
    IReadOnlyList<PreparationSchemeResponse> Schemes);

public sealed record PreparationSchemeResponse(
    Guid MonthlyDistributionSchemeId,
    string SchemeIdentifier,
    string SchemeName,
    string DistributionReferenceFull,
    int NumberOfSetsRequired,
    string SamplePackingInstructions,
    DateTime? DiaryEntry,
    DateTime? PrepStartDate,
    DateTime? PrepCompleteDate,
    IReadOnlyList<PreparationSampleResponse> Samples);

public sealed record PreparationSampleResponse(Guid SampleId, string SampleNumber, string InternalDescription);

public sealed record PreparationSaveRequest(IReadOnlyList<PreparationStageUpdateRequest> Updates);

public sealed record PreparationStageUpdateRequest(
    Guid MonthlyDistributionSchemeId,
    bool DiaryEntryChecked,
    bool PrepStartChecked,
    bool PrepCompleteChecked);

public sealed record PreparationSaveResult(bool Success);
