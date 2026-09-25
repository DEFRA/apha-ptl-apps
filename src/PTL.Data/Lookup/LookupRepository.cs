using Dapper;
using PTL.Core.Lookup;
using PTL.Data.Infrastructure;

namespace PTL.Data.Lookup;

public sealed class LookupRepository(IDbConnectionFactory connectionFactory) : ILookupRepository
{
    public async Task<IReadOnlyList<CountryEntity>> GetCountriesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CountryEntity>("EXEC dbo.spgaCountry")).ToList();
    }

    public async Task<IReadOnlyList<CurrencyEntity>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CurrencyEntity>("EXEC dbo.spgaCurrency")).ToList();
    }

    public async Task<IReadOnlyList<CustomerTypeEntity>> GetCustomerTypesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<CustomerTypeEntity>("EXEC dbo.spgaCustomerType")).ToList();
    }

    public async Task<IReadOnlyList<VatRatingEntity>> GetVatRatingsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<VatRatingEntity>("EXEC dbo.spgaVatRating")).ToList();
    }

    public async Task<IReadOnlyList<LabTypeEntity>> GetLabTypesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<LabTypeEntity>("EXEC dbo.spgaLabType")).ToList();
    }

    public async Task<IReadOnlyList<YearEntity>> GetCurrentYearsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<YearEntity>("EXEC dbo.spgaYearCurrent")).ToList();
    }

    public async Task<IReadOnlyList<YearEntity>> GetAllYearsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<YearEntity>("EXEC dbo.spgaYear")).ToList();
    }

    public async Task<IReadOnlyList<YearEntity>> GetWeightedPricingYearsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<YearEntity>("EXEC dbo.spgaWeightedPricingYear")).ToList();
    }

    public async Task<IReadOnlyList<GroupAddressEntity>> GetGroupAddressesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<GroupAddressEntity>("EXEC dbo.spgaGroupAddress")).ToList();
    }

    public async Task<IReadOnlyList<SchemeCurrencyEntity>> GetSchemeCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<SchemeCurrencyEntity>("EXEC dbo.spgaSchemeCurrency")).ToList();
    }

    public async Task<IReadOnlyList<PostagePricingPlanEntity>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<PostagePricingPlanEntity>(
            "EXEC dbo.spgPostageByYearID @YearId",
            new { YearId = yearId })).ToList();
    }

    public async Task<SystemSettingsEntity> GetSystemSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryFirstAsync<SystemSettingsEntity>("EXEC dbo.spgaSystemSettings");
    }
}
