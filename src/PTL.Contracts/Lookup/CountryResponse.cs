namespace PTL.Contracts.Lookup;

// Public API contract for GET /api/lookups/countries; matches the legacy DropDownCountry binding
// (SystemObjects.CountryCollection / spgaCountry, DataTextField "Country", DataValueField "CountryId").
public sealed record CountryResponse(Guid CountryId, string Country);
