namespace PTL.Core.Country;

// Keyless domain projection for the rows returned by spgaCountryType (EU/NonEU/UK/None).
public sealed class CountryTypeEntity
{
    public Guid CountryTypeId { get; set; }
    public string CountryType { get; set; } = string.Empty;
}
