using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.Distribution;

// Distribution Participants screen - legacy Scheduling/DistributionParticipantList.aspx. Reached
// from the Scheduling screen's "View Participants" link.
public sealed class DistributionParticipantsViewModel
{
#pragma warning disable S6964
    public Guid MonthlyDistributionSchemeId { get; set; }

    public int YearId { get; set; }

    public int MonthId { get; set; }
#pragma warning restore S6964

    public string? SchemeIdentifier { get; set; }

    public string? SchemeName { get; set; }

    public string? DistributionReferenceFull { get; set; }

    public string SchemeHeading => $"{SchemeIdentifier}: {SchemeName}";

    // Legacy "Original Distribution"/"Repeat Distribution" (lblDistributionListTitle) - a repeat
    // distribution never offers a further repeat (confirmed business rule, monthlys.aspx.vb).
#pragma warning disable S6964
    public bool IsRepeatDistribution { get; set; }
#pragma warning restore S6964

    public string DistributionListTitle => IsRepeatDistribution ? "Repeat Distribution" : "Original Distribution";

    public List<DistributionParticipantRowViewModel> Participants { get; set; } = [];

    public List<DistributionAdditionalParticipantRowViewModel> AdditionalParticipants { get; set; } = [];

    // Legacy hides btnRepeatDistribution entirely once already a repeat, or when there is nothing
    // to repeat.
    public bool ShowRepeatDistributionButton =>
        !IsRepeatDistribution && (Participants.Count > 0 || AdditionalParticipants.Count > 0);
}

public sealed class DistributionParticipantRowViewModel
{
#pragma warning disable S6964
    public Guid DistributionParticipantId { get; set; }

    public bool AdditionalParticipant { get; set; }
#pragma warning restore S6964

    public string? FullName { get; set; }

    public DateTime DeadlineDate { get; set; }

    public string? DeadlineChangeReason { get; set; }

    // Select All / repeat-distribution checkbox - round-tripped so a redisplay after a failed
    // Change Deadline Date save does not silently clear other rows' selections.
#pragma warning disable S6964
    public bool Selected { get; set; }
#pragma warning restore S6964

    // New Date/Reason are deliberately NOT [Required] here - legacy scopes each row's validators to
    // its own ValidationGroup, so only the row whose Save link was actually clicked is validated;
    // the InternalWeb DistributionController.ChangeDeadline action enforces this manually.
    [Display(Name = "New Date")]
    [DataType(DataType.Date)]
    public DateTime? NewDeadlineDate { get; set; }

    [Display(Name = "Reason")]
    public string? NewDeadlineReason { get; set; }
}

public sealed class DistributionAdditionalParticipantRowViewModel
{
#pragma warning disable S6964
    public Guid ParticipantSchemeId { get; set; }

    public bool Selected { get; set; }
#pragma warning restore S6964

    public string? FullName { get; set; }
}
