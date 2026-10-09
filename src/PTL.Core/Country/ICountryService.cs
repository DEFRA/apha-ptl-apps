namespace PTL.Core.Country;

public interface ICountryService
{
    Task<IReadOnlyList<Country>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CountryTypeEntity>> GetCountryTypesAsync(CancellationToken cancellationToken = default);
    Task<Country> CreateAsync(string country, Guid countryTypeId, CancellationToken cancellationToken = default);
    Task<Country?> UpdateAsync(Guid countryId, string country, Guid countryTypeId, CancellationToken cancellationToken = default);

    // Blocks removal (Success = false, Message populated) if the country is still referenced by
    // any Customer/Participant/GroupAddress record - matches legacy GridViewCountry_RowDeleting.
    Task<CountryDeleteResult> DeleteAsync(Guid countryId, CancellationToken cancellationToken = default);
}
