namespace PTL.Core.Country;

public interface ICountryRepository
{
    // spgaCountry
    Task<IReadOnlyList<Country>> GetAllAsync(CancellationToken cancellationToken = default);

    // spgaCountryType
    Task<IReadOnlyList<CountryTypeEntity>> GetCountryTypesAsync(CancellationToken cancellationToken = default);

    // spiCountry
    Task CreateAsync(Country country, CancellationToken cancellationToken = default);

    // spuCountry
    Task UpdateAsync(Country country, CancellationToken cancellationToken = default);

    // spdCountry
    Task DeleteAsync(Guid countryId, CancellationToken cancellationToken = default);
}
