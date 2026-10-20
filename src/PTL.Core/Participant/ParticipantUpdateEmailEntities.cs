namespace PTL.Core.Participant;

// Row shape returned by spgaUpdatedParticipantDetailsEmail (tblUpdatedParticipantDetailsEmail, fldDateSent null). The
// procedure also looks up the QAL number from the lab code, which the email subject uses.
public class ParticipantUpdateEmailEntity
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string LabCode { get; set; } = string.Empty;
    public Guid PendingParticipantUpdateId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public string? QalNumber { get; set; }
}

// One row of tblUpdatedParticipantDetailsEmail (legacy PendingParticipantUpdate.EmailUpdateNotifications).
public sealed record ParticipantUpdateNotification(
    string Email,
    string LabCode,
    Guid PendingParticipantUpdateId,
    string ParticipantName,
    string Notes,
    bool IsApproved);

// Replaces the legacy Windows service's SendParticipantUpdateEmails switch; its timer is now NotificationSchedulerOptions.
public sealed class ParticipantUpdateEmailOptions
{
    public const string SectionName = "ParticipantUpdateEmails";

    public bool Enabled { get; set; } = true;

    // GOV.UK Notify template ids (participant-update-accepted / participant-update-declined); a row stays unsent while its id is blank.
    public string AcceptedTemplateId { get; set; } = string.Empty;

    public string DeclinedTemplateId { get; set; } = string.Empty;
}
