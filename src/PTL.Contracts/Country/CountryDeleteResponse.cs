namespace PTL.Contracts.Country;

// Returned by DELETE /api/countries/{countryId} - Success is false (with Message populated)
// both when the country is still referenced by a Customer/Participant/GroupAddress record and
// when the country could not be found, matching legacy's dependency-count warning message
// ("This country is being used by N customer(s)/participant(s)/Group Addresses.").
public sealed record CountryDeleteResponse(bool Success, string? Message);
