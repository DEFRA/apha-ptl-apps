namespace PTL.Core.AdministrationCharge;

// Keyless domain projection for the rows returned by spgaAdministrationCharge (tblAdministrationCharge).
// The charge name is fixed reference data - no legacy screen creates or renames it (mirrors
// PtaBusinessObjects.BusinessObjects.SystemObjects.AdministrationCharge).
public class AdministrationChargeEntity
{
    public Guid AdministrationChargeId { get; set; }
    public string Name { get; set; } = string.Empty;
}
