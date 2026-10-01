namespace PTL.Core.Lookup;

// spgaGroupAddress result projection (tblGroupAddress) - matches legacy
// PtaBusinessObjects.BusinessObjects.Contracts.GroupAddress, used by ParticipantScheme.aspx's
// "Select a Group Address" popup. Only the fields shown on that screen are modelled here.
public class GroupAddressEntity
{
    public Guid GroupAddressId { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public Guid CountryId { get; set; }
}
