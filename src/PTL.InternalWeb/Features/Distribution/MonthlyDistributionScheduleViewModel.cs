using System.ComponentModel.DataAnnotations;
using PTL.Core.Distribution;

namespace PTL.InternalWeb.Features.Distribution;

// Monthly Distributions (Scheduling) screen - legacy Scheduling/monthlys.aspx. One row per scheme
// distributing in the selected month; every row's dates are always editable (legacy has no
// per-row "click Edit first" step), submitted together via the page-level Save/Apply buttons.
public sealed class MonthlyDistributionScheduleRowViewModel
{
    // S6964 (value-type controller-action input) suppressed for this block: these are hidden inputs
    // and HTML checkboxes round-tripped verbatim so the Cancel/Commence toggle can redisplay the
    // form without losing unsaved edits - making them nullable would break that round-trip.
#pragma warning disable S6964
    public Guid MonthlyDistributionSchemeId { get; set; }
#pragma warning restore S6964

    public string SchemeIdentifier { get; set; } = string.Empty;

    public string SchemeName { get; set; } = string.Empty;

    public string DistributionReferenceFull { get; set; } = string.Empty;

    [Required(ErrorMessage = "Distribution Date required.")]
    [Display(Name = "Distribution")]
    [DataType(DataType.Date)]
    public DateTime? DistributionDate { get; set; }

    [Required(ErrorMessage = "Overseas Posting Date required.")]
    [Display(Name = "O/seas Posting")]
    [DataType(DataType.Date)]
    public DateTime? OverseasPostingDate { get; set; }

    [Required(ErrorMessage = "Deadline Date required.")]
    [Display(Name = "Deadline")]
    [DataType(DataType.Date)]
    public DateTime? DeadlineDate { get; set; }

    [Required(ErrorMessage = "Results Issue Target Date required.")]
    [Display(Name = "Results")]
    [DataType(DataType.Date)]
    public DateTime? ResultsIssueTargetDate { get; set; }

#pragma warning disable S6964
    public bool IsCancelled { get; set; }

    public bool IsAsAvailable { get; set; }

    public int ParticipantCount { get; set; }

    public int TotalSetsOfSamplesRequired { get; set; }

    public bool HasSampleNumbersDefined { get; set; }

    public bool HasIntendedResults { get; set; }
#pragma warning restore S6964

    // "[Cancelled] "/"[Not Commenced] " heading prefix - legacy dlMonth_RowDataBound.
    public string SchemeHeading
    {
        get
        {
            if (!IsCancelled)
            {
                return $"{SchemeIdentifier}: {SchemeName}";
            }

            var prefix = IsAsAvailable ? "[Not Commenced]" : "[Cancelled]";
            return $"{prefix} {SchemeIdentifier}: {SchemeName}";
        }
    }

    // A cancelled, non-"as available" scheme has no Commence option at all (legacy hides the link
    // entirely - HyperLink_Cancel.Visible = False).
    public bool ShowCancelCommenceLink => !IsCancelled || IsAsAvailable;

    public string CancelCommenceLinkText => IsCancelled ? "Commence Distribution" : "Cancel this Distribution";

    public string CancelCommenceConfirmText => IsCancelled
        ? "Are you sure you wish to Commence this Distribution?"
        : "Are you sure you wish to Cancel this Distribution?";

    public string ParticipantCountLabel => ParticipantCount == 1 ? "1 participant" : $"{ParticipantCount} participants";

    public string SampleNumbersLinkText => HasSampleNumbersDefined ? "View Sample Numbers" : "Define Sample Numbers";

    // Legacy never updates this tooltip when toggling to "View" mode - preserved as-is.
    public const string SampleNumbersTooltip = "Click to define the Sample Numbers for this Scheme.";

    public bool SampleNumbersLinkEnabled => ParticipantCount > 0;

    public string? SampleNumbersDisabledTooltip => ParticipantCount == 0
        ? "Sample numbers not required as this Scheme has no participants for this month."
        : null;

    public bool IntendedResultsLinkEnabled => HasSampleNumbersDefined;

    public string IntendedResultsTooltip => HasSampleNumbersDefined
        ? "Click to enter the Intended Results for this Scheme."
        : "Must define Sample Numbers first.";
}

public sealed class MonthlyDistributionScheduleViewModel : IValidatableObject
{
    // S6964 suppressed: both are hidden inputs, and the controller overwrites them from the route
    // values on every path before they are read.
#pragma warning disable S6964
    public int YearId { get; set; }

    public int MonthId { get; set; }
#pragma warning restore S6964

    // "MMM yyyy" - legacy lblMonthYear.
    public string MonthYearLabel { get; set; } = string.Empty;

    public List<MonthlyDistributionScheduleRowViewModel> Schemes { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        for (var i = 0; i < Schemes.Count; i++)
        {
            var row = Schemes[i];
            if (row.DistributionDate is null || row.OverseasPostingDate is null || row.DeadlineDate is null || row.ResultsIssueTargetDate is null)
            {
                // [Required] on the row already reports these - the ordering rules below need all
                // four values present to mean anything.
                continue;
            }

            var errors = MonthlyDistributionScheduleValidator.Validate(
                row.DistributionDate.Value, row.OverseasPostingDate.Value, row.DeadlineDate.Value, row.ResultsIssueTargetDate.Value);

            foreach (var error in errors)
            {
                yield return new ValidationResult(error.Message, [$"{nameof(Schemes)}[{i}].{error.Field}"]);
            }
        }
    }
}
