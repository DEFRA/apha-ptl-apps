namespace PTL.Contracts.Country;

// Public API contract for the Country Management admin screen (GET/POST/PUT/DELETE
// /api/countries) - a richer sibling of PTL.Contracts.Lookup.CountryResponse (which stays a thin
// CountryId/Country projection for the many unrelated dropdown consumers). Matches the legacy
// Admin/ManageCountries.aspx grid (Name, Country Type, Actions) plus the AllocationCount already
// returned by spgaCountry, used to block removal of an in-use country.
public sealed record CountryResponse(Guid CountryId, string Country, Guid CountryTypeId, string CountryType, int AllocationCount);
