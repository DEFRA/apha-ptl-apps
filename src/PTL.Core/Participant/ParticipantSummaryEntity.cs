namespace PTL.Core.Participant;

public class ParticipantSummaryEntity
{
    public Guid ParticipantId { get; set; }
    public Guid CustomerId { get; set; }
    public string LabCode { get; set; } = string.Empty;
    public string LabName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
