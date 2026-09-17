namespace PTL.Core.Participant;

public class Participant
{
    public Guid ParticipantId { get; set; }
    public Guid SsoId { get; set; }
    public Guid CustomerId { get; set; }
    public string LabCode { get; set; } = string.Empty;
    public string LabName { get; set; } = string.Empty;
    public Guid LabTypeId { get; set; }
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
    public string Comments { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? InactiveDate { get; set; }
    public bool InactiveError { get; set; }
    public DateTime? InactiveErrorDate { get; set; }
}
