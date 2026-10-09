namespace PTL.Contracts.Country;

// GET /api/countries/types - EU/NonEU/UK/None, for the Country Type dropdown on the Country
// Management screen (spgaCountryType).
public sealed record CountryTypeResponse(Guid CountryTypeId, string CountryType);
