namespace PTL.Core.Participant;

// Domain entity mapped to tblPendingParticipantDetailsEdit - a participant-proposed change to
// their own record awaiting admin approval/decline (legacy PendingParticipantUpdate.vb /
// spgPendingParticipantDetailsEditByParticipantID). Only IsSubmitted=1, IsDeleted=0 rows are
// "live" pending updates - see IPendingParticipantUpdateRepository.
public class PendingParticipantUpdate
{
    public Guid PendingParticipantUpdateId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid CustomerId { get; set; }
    public string LabCode { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Organisation { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string Address3 { get; set; } = string.Empty;
    public string Address4 { get; set; } = string.Empty;
    public string Address5 { get; set; } = string.Empty;
    public Guid CountryId { get; set; }
    public string Telephone { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Email2 { get; set; } = string.Empty;
    public bool IsSubmitted { get; set; }
    public bool IsDeleted { get; set; }
}
