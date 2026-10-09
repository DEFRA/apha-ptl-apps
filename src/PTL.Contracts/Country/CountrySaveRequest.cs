namespace PTL.Contracts.Country;

// Posted to create (POST /api/countries) or edit (PUT /api/countries/{countryId}) a country -
// matches legacy ButtonAdd_Click / GridViewCountry_Updating (Name + Country Type only).
public sealed record CountrySaveRequest(string Country, Guid CountryTypeId);
