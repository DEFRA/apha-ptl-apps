using PTL.Core.Audit;

namespace PTL.Core.Country;

public interface ICountryRepository
{
    // spgaCountry
    Task<IReadOnlyList<Country>> GetAllAsync(CancellationToken cancellationToken = default);

    // spgaCountryType
    Task<IReadOnlyList<CountryTypeEntity>> GetCountryTypesAsync(CancellationToken cancellationToken = default);

    // spiCountry
    Task CreateAsync(Country country, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default);

    // spuCountry
    Task UpdateAsync(Country country, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default);

    // spdCountry
    Task DeleteAsync(Guid countryId, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default);
}
