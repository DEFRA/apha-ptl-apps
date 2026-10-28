using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.ExternalWeb.Features.Participant;

/// <summary>View model for the participant-facing Edit Participant Details screen (legacy
/// EditParticipantDetails.aspx).</summary>
public sealed class ParticipantDetailsEditViewModel
{
    public string LabCode { get; set; } = string.Empty;

    /// <summary>True when a submitted-but-undecided pending update already exists - the form is
    /// shown read-only with a notice (legacy DisableForm()/NotificationMessage).</summary>
    public bool HasOutstandingUpdate { get; set; }

    // Nullable (not just defaulted to "") so a blank post doesn't also trigger ASP.NET Core's
    // implicit non-nullable-reference-type [Required] check alongside PendingParticipantUpdateValidator's.
    public string? ContactName { get; set; } = string.Empty;
    public string? Organisation { get; set; } = string.Empty;
    public string? Address1 { get; set; } = string.Empty;
    public string? Address2 { get; set; } = string.Empty;
    public string? Address3 { get; set; } = string.Empty;
    public string? Address4 { get; set; } = string.Empty;
    public string? Address5 { get; set; } = string.Empty;
    public Guid? CountryId { get; set; }
    public string? Telephone { get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public string? Email2 { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> CountryOptions { get; set; } = [];
}
