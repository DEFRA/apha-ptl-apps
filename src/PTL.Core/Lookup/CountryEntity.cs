namespace PTL.Core.Lookup;

// Keyless domain projection for the list returned by spgaCountry.
public class CountryEntity
{
    public Guid CountryId { get; set; }
    public string Country { get; set; } = string.Empty;
}
