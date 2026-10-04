namespace PTL.Core.Participant;

// Row shape returned by spgaPendingParticipantDetailsEditInfo - one per outstanding pending
// participant update, joined with tblParticipant for display (legacy
// ReviewPendingParticipantUpdates.aspx grid).
public class PendingParticipantUpdateSummaryEntity
{
    public Guid ParticipantId { get; set; }
    public Guid PendingParticipantUpdateId { get; set; }
    public string LabCode { get; set; } = string.Empty;
    public string LabName { get; set; } = string.Empty;
}
