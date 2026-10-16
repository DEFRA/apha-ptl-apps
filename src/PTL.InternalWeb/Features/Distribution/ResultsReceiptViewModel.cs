namespace PTL.InternalWeb.Features.Distribution;

// Results Receipt screen - legacy Internal User/ResultsReceipt.aspx. Reached from the Dashboard's
// "Results: View" link.
public sealed class ResultsReceiptViewModel
{
#pragma warning disable S6964
    public int YearId { get; set; }

    public int MonthId { get; set; }
#pragma warning restore S6964

    // Legacy page title "Results Receipt - <MonthName> <Year>".
    public string MonthDescription { get; set; } = string.Empty;

    public List<ResultsReceiptSchemeRowViewModel> Schemes { get; set; } = [];
}

public sealed class ResultsReceiptSchemeRowViewModel
{
#pragma warning disable S6964
    public Guid MonthlyDistributionSchemeId { get; set; }
#pragma warning restore S6964

    // Legacy "<Reference>/<Code>: <Identifier> <Name>" scheme row text.
    public string Description { get; set; } = string.Empty;

    // Legacy: Submission Status and Returned to Participant only apply to Schemes requiring assessment.
    public bool RequiresAssessment { get; set; }

    // Legacy progress bar: share of participants who have a First Edit date.
    public int ProgressPercentage { get; set; }

    public List<ResultsReceiptParticipantRowViewModel> Participants { get; set; } = [];
}

public sealed class ResultsReceiptParticipantRowViewModel
{
#pragma warning disable S6964
    public Guid DistributionParticipantId { get; set; }
#pragma warning restore S6964

    // Legacy "<LabCode>: <LabName>".
    public string FullName { get; set; } = string.Empty;

    public DateTime DeadlineDate { get; set; }

    // Null = legacy "No Results" (spans First Edit and Last Edit).
    public DateTime? FirstEditDate { get; set; }

    public DateTime? LastEditDate { get; set; }

    // Posted back: the Extra Documents tick box.
    public bool ExtraDocumentsReturned { get; set; }

    // False = the legacy View/Edit links are disabled with a tooltip (not yet posted / no Sample Numbers).
    public bool CanViewResults { get; set; }

    // Legacy shows " (Y)" when every sample is accounted for and " (N)" when not.
    public bool HasUnaccountedSamples { get; set; }

    // Set = shown as a date; null (assessment schemes only) = the Returned to Participant tick box.
    public DateTime? ReturnedDate { get; set; }

    // Posted back: the Returned to Participant tick box - only rendered while ReturnedDate is null.
    public bool ReturnedChecked { get; set; }
}
