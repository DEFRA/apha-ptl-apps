namespace PTL.Core.Distribution;

// spgPreparationSheet's scheme result set - legacy
// PtaBusinessObjects.BusinessObjects.Distributions.Preparation.MonthlyDistributionScheme. Diary
// Entry/Prep Started/Prep Completed are each null until their checkbox is ticked and saved - once
// set they are never cleared or overwritten (see PreparationService's cascade rule).
public sealed class PreparationSchemeEntity
{
    public Guid MonthlyDistributionSchemeId { get; set; }
    public string SchemeIdentifier { get; set; } = string.Empty;
    public string SchemeName { get; set; } = string.Empty;
    public string DistributionReference { get; set; } = string.Empty;
    public string DistributionReferenceSuffix { get; set; } = string.Empty;
    public string ScheduleCode { get; set; } = string.Empty;
    public int NumberOfSetsRequired { get; set; }
    public string SamplePackingInstructions { get; set; } = string.Empty;
    public DateTime? DiaryEntry { get; set; }
    public DateTime? PrepStartDate { get; set; }
    public DateTime? PrepCompleteDate { get; set; }
    public List<PreparationSampleEntity> Samples { get; set; } = [];

    // Legacy MonthlyDistributionScheme.vb: Reference + Suffix + "/" + ScheduleCode (e.g. "17152/BA").
    public string DistributionReferenceFull => $"{DistributionReference}{DistributionReferenceSuffix}/{ScheduleCode}";
}

// spgPreparationSheet's sample result set - legacy Sample.vb (Preparation variant). Only the two
// fields PreparationSheet.aspx actually renders (SampleNumber: InternalDescription) are carried -
// ExternalDescription/TabulationDescription/TestConsultantsDescription exist on the legacy object
// but are never used by this screen.
public sealed class PreparationSampleEntity
{
    public Guid MonthlyDistributionSchemeId { get; set; }
    public Guid SampleId { get; set; }
    public string SampleNumber { get; set; } = string.Empty;
    public string InternalDescription { get; set; } = string.Empty;
}

// spgPreparationSheet's header row (first result set) - legacy PreparationSheet.DataPortal_Fetch.
public sealed class PreparationHeaderEntity
{
    public int YearId { get; set; }
    public int MonthId { get; set; }
}

public sealed record PreparationSheetData(
    Guid MonthlyDistributionId,
    int YearId,
    int MonthId,
    IReadOnlyList<PreparationSchemeEntity> Schemes);
