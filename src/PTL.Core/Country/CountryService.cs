namespace PTL.Core.Country;

public sealed class CountryService(ICountryRepository repository) : ICountryService
{
    public Task<IReadOnlyList<Country>> GetAllAsync(CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(cancellationToken);

    public Task<IReadOnlyList<CountryTypeEntity>> GetCountryTypesAsync(CancellationToken cancellationToken = default) =>
        repository.GetCountryTypesAsync(cancellationToken);

    public async Task<Country> CreateAsync(string country, Guid countryTypeId, CancellationToken cancellationToken = default)
    {
        var name = country.Trim();
        Validate(name, countryTypeId);

        var existing = await repository.GetAllAsync(cancellationToken);
        EnsureNameNotDuplicated(existing, name, excludingCountryId: null);

        var newCountry = new Country { CountryId = Guid.NewGuid(), CountryName = name, CountryTypeId = countryTypeId };
        await repository.CreateAsync(newCountry, cancellationToken);
        return newCountry;
    }

    public async Task<Country?> UpdateAsync(Guid countryId, string country, Guid countryTypeId, CancellationToken cancellationToken = default)
    {
        var existingCountries = await repository.GetAllAsync(cancellationToken);
        var existing = existingCountries.FirstOrDefault(c => c.CountryId == countryId);
        if (existing is null)
        {
            return null;
        }

        var name = country.Trim();
        Validate(name, countryTypeId);
        EnsureNameNotDuplicated(existingCountries, name, excludingCountryId: countryId);

        var updated = new Country { CountryId = countryId, CountryName = name, CountryTypeId = countryTypeId };
        await repository.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<CountryDeleteResult> DeleteAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        var existing = (await repository.GetAllAsync(cancellationToken)).FirstOrDefault(c => c.CountryId == countryId);
        if (existing is null)
        {
            return new CountryDeleteResult(false, "Country not found.");
        }

        if (existing.AllocationCount > 0)
        {
            return new CountryDeleteResult(false, $"This country is being used by {existing.AllocationCount} customer(s)/participant(s)/Group Addresses.");
        }

        await repository.DeleteAsync(countryId, cancellationToken);
        return new CountryDeleteResult(true, null);
    }

    private static void Validate(string country, Guid countryTypeId)
    {
        var result = CountryValidator.Validate(country, countryTypeId);
        if (!result.IsValid)
        {
            throw new CountryValidationException(result.Errors);
        }
    }

    // Case-insensitive, matching legacy ButtonAdd_Click's duplicate check and the
    // UQ_tblCountry_fldCountry unique constraint it exists to avoid tripping.
    private static void EnsureNameNotDuplicated(IEnumerable<Country> existing, string name, Guid? excludingCountryId)
    {
        var isDuplicate = existing.Any(c =>
            c.CountryId != excludingCountryId &&
            string.Equals(c.CountryName, name, StringComparison.OrdinalIgnoreCase));

        if (isDuplicate)
        {
            throw new CountryValidationException([new CountryValidationError("Country", CountryValidator.DuplicateNameMessage)]);
        }
    }
}
