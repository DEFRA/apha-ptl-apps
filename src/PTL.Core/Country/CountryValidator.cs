namespace PTL.Core.Country;

// Required-field validation only - matches legacy ValidatorNewNameRequired/ValidatorNameRequired
// (Country Type is never actually missing in the legacy UI, since its dropdown always has a
// pre-selected value, but the API must still validate independently of the UI).
public static class CountryValidator
{
    // Preserves legacy's exact ButtonAdd_Click duplicate-name message text.
    public const string DuplicateNameMessage = "This country already exists. Please try with different name.";

    public static CountryValidationResult Validate(string country, Guid countryTypeId)
    {
        var errors = new List<CountryValidationError>();

        if (string.IsNullOrWhiteSpace(country))
        {
            errors.Add(new CountryValidationError("Country", "Enter a country name"));
        }
        else if (country.Length > 50)
        {
            errors.Add(new CountryValidationError("Country", "Country name must be 50 characters or fewer"));
        }

        if (countryTypeId == Guid.Empty)
        {
            errors.Add(new CountryValidationError(nameof(Country.CountryTypeId), "Select a country type"));
        }

        return new CountryValidationResult(errors.Count == 0, errors);
    }
}

public sealed record CountryValidationError(string Field, string Message);
public sealed record CountryValidationResult(bool IsValid, IReadOnlyList<CountryValidationError> Errors);
