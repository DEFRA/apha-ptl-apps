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

// Replaces the legacy Windows service's SendParticipantUpdateEmails switch.
public sealed class ParticipantUpdateEmailOptions
{
    public const string SectionName = "ParticipantUpdateEmails";

    public bool Enabled { get; set; } = true;
}

// Fixed - the same GOV.UK Notify templates are used in every environment, so they are not configuration.
public static class ParticipantUpdateEmailTemplates
{
    public const string AcceptedName = "participant-update-accepted";
    public const string AcceptedId = "9629ea12-26a4-4b64-a071-516bb26ca446";

    public const string DeclinedName = "participant-update-declined";
    public const string DeclinedId = "2c1db141-ad62-4acc-bfa5-c81665061107";
}
