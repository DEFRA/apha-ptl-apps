namespace PTL.Core.Lookup;

// Keyless domain projection for the list returned by spgaCountry.
public class CountryEntity
{
    public Guid CountryId { get; set; }
    public string Country { get; set; } = string.Empty;

    // tblCountryType.fldCountryType ("UK" / "EU" / non-EU) - selects which of a postage pricing
    // plan's three prices applies, per legacy PendingContractOrder.aspx.vb getCountryType.
    public string CountryType { get; set; } = string.Empty;
}
