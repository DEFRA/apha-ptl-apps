using PTL.Core.Country;

namespace PTL.Api.Tests.Country;

internal sealed class FakeCountryRepository : ICountryRepository
{
    public List<PTL.Core.Country.Country> Countries { get; set; } = [];
    public List<CountryTypeEntity> CountryTypes { get; set; } = [];
    public List<PTL.Core.Country.Country> Created { get; } = [];
    public List<PTL.Core.Country.Country> Updated { get; } = [];
    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<PTL.Core.Country.Country>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PTL.Core.Country.Country>>(Countries);

    public Task<IReadOnlyList<CountryTypeEntity>> GetCountryTypesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CountryTypeEntity>>(CountryTypes);

    public Task CreateAsync(PTL.Core.Country.Country country, CancellationToken cancellationToken = default)
    {
        Created.Add(country);
        Countries.Add(country);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(PTL.Core.Country.Country country, CancellationToken cancellationToken = default)
    {
        Updated.Add(country);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        Deleted.Add(countryId);
        Countries.RemoveAll(c => c.CountryId == countryId);
        return Task.CompletedTask;
    }
}
