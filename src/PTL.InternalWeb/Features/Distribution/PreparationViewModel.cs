namespace PTL.InternalWeb.Features.Distribution;

// Sample Preparation screen - legacy Internal User/PreparationSheet.aspx. Reached from the
// Dashboard's "Preparation: View" link.
public sealed class PreparationViewModel
{
#pragma warning disable S6964
    public int YearId { get; set; }

    public int MonthId { get; set; }
#pragma warning restore S6964

    // Legacy LblPrintTitle1 "Sample Preparation - <MonthName> <Year>".
    public string MonthDescription { get; set; } = string.Empty;

    public List<PreparationSchemeRowViewModel> Schemes { get; set; } = [];
}

public sealed class PreparationSchemeRowViewModel
{
#pragma warning disable S6964
    public Guid MonthlyDistributionSchemeId { get; set; }
#pragma warning restore S6964

    public string SchemeIdentifier { get; set; } = string.Empty;

    public string SchemeName { get; set; } = string.Empty;

    public string DistributionReferenceFull { get; set; } = string.Empty;

    // Legacy: DistributionReferenceFull + " " + SchemeIdentifier + ": " + SchemeName.
    public string SchemeHeading => $"{DistributionReferenceFull} {SchemeIdentifier}: {SchemeName}";

    public int NumberOfSetsRequired { get; set; }

    public string SamplePackingInstructions { get; set; } = string.Empty;

    // Legacy: a scheme with no Sample Numbers yet defined shows "The Sample Numbers for this
    // Scheme have not yet been defined" instead of Diary Entry/Prep Started/Prep Completed/Results.
    public bool HasSamples => Samples.Count > 0;

    public List<PreparationSampleRowViewModel> Samples { get; set; } = [];

    // Null until the corresponding checkbox is ticked and saved - once set, never cleared
    // (legacy SetDiaryEntryStarted/SetPrepStarted/SetPrepCompleted are each idempotent).
    public DateTime? DiaryEntry { get; set; }

    public DateTime? PrepStartDate { get; set; }

    public DateTime? PrepCompleteDate { get; set; }

    // Posted checkbox state - only rendered/bound while the matching date above is still null.
    public bool DiaryEntryChecked { get; set; }

    public bool PrepStartChecked { get; set; }

    public bool PrepCompleteChecked { get; set; }

    // "Print" selection checkbox - legacy Checkbox_Print, used client-side only to control which
    // scheme rows are included when the browser print dialog opens.
    public bool Selected { get; set; }
}

public sealed class PreparationSampleRowViewModel
{
    public string SampleNumber { get; set; } = string.Empty;

    public string InternalDescription { get; set; } = string.Empty;
}
