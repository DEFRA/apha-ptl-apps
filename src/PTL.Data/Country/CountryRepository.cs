using Dapper;
using PTL.Core.Country;
using PTL.Data.Infrastructure;
using CoreCountry = PTL.Core.Country.Country;

namespace PTL.Data.Country;

// The stored procedures are unchanged legacy objects (spgaCountry, spgaCountryType, spiCountry,
// spuCountry, spdCountry) - this only adds a Dapper-based access path to them.
public sealed class CountryRepository(IDbConnectionFactory connectionFactory) : ICountryRepository
{
    public async Task<IReadOnlyList<CoreCountry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CoreCountry>("EXEC dbo.spgaCountry")).ToList();
    }

    public async Task<IReadOnlyList<CountryTypeEntity>> GetCountryTypesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CountryTypeEntity>("EXEC dbo.spgaCountryType")).ToList();
    }

    public async Task CreateAsync(CoreCountry country, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spiCountry @CountryId, @Country, @CountryTypeId",
            new { country.CountryId, Country = country.CountryName, country.CountryTypeId });
    }

    public async Task UpdateAsync(CoreCountry country, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "EXEC dbo.spuCountry @CountryId, @Country, @CountryTypeId",
            new { country.CountryId, Country = country.CountryName, country.CountryTypeId });
    }

    public async Task DeleteAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("EXEC dbo.spdCountry @CountryId", new { CountryId = countryId });
    }
}
