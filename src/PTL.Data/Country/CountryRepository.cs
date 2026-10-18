using Dapper;
using PTL.Core.Audit;
using PTL.Core.Country;
using PTL.Data.Audit;
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

    public Task CreateAsync(CoreCountry country, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default) =>
        AuditTrailWriter.ExecuteAsync(
            connectionFactory,
            auditEntry,
            (connection, transaction) => connection.ExecuteAsync(
                "EXEC dbo.spiCountry @CountryId, @Country, @CountryTypeId",
                new { country.CountryId, Country = country.CountryName, country.CountryTypeId },
                transaction),
            cancellationToken);

    public Task UpdateAsync(CoreCountry country, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default) =>
        AuditTrailWriter.ExecuteAsync(
            connectionFactory,
            auditEntry,
            (connection, transaction) => connection.ExecuteAsync(
                "EXEC dbo.spuCountry @CountryId, @Country, @CountryTypeId",
                new { country.CountryId, Country = country.CountryName, country.CountryTypeId },
                transaction),
            cancellationToken);

    public Task DeleteAsync(Guid countryId, AuditEntry? auditEntry = null, CancellationToken cancellationToken = default) =>
        AuditTrailWriter.ExecuteAsync(
            connectionFactory,
            auditEntry,
            (connection, transaction) => connection.ExecuteAsync("EXEC dbo.spdCountry @CountryId", new { CountryId = countryId }, transaction),
            cancellationToken);
}
