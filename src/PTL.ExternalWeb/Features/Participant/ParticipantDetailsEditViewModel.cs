using PTL.ExternalWeb.Features.MyAccount;

namespace PTL.ExternalWeb.Features.Participant;

/// <summary>View model for the participant-facing Edit Participant Details screen (legacy
/// EditParticipantDetails.aspx).</summary>
public sealed class ParticipantDetailsEditViewModel : AccountDetailsEditViewModelBase
{
    public string LabCode { get; set; } = string.Empty;

    public string? Email2 { get; set; } = string.Empty;
}
