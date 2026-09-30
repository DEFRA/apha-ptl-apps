using Dapper;
using PTL.Core.AdministrationCharge;
using PTL.Data.Infrastructure;

namespace PTL.Data.AdministrationCharge;

// The stored procedures are unchanged legacy objects (spga/spg/spi/spu for AdministrationCharge and
// AdministrationChargeCurrency) - this only adds a Dapper-based access path to them.
public sealed class AdministrationChargeRepository(IDbConnectionFactory connectionFactory) : IAdministrationChargeRepository
{
    public async Task<IReadOnlyList<AdministrationChargeEntity>> GetChargesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<AdministrationChargeEntity>("EXEC dbo.spgaAdministrationCharge")).ToList();
    }

    public async Task<IReadOnlyList<AdministrationChargeCurrencyEntity>> GetPricesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return (await connection.QueryAsync<AdministrationChargeCurrencyEntity>("EXEC dbo.spgaAdministrationChargeCurrency")).ToList();
    }

    public async Task<AdministrationChargeCurrencyEntity?> GetPriceAsync(Guid administrationChargeId, Guid currencyId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<AdministrationChargeCurrencyEntity>(
            "EXEC dbo.spgAdministrationChargeCurrencyByAdministrationChargeIdCurrencyId @AdministrationChargeId, @CurrencyId",
            new { AdministrationChargeId = administrationChargeId, CurrencyId = currencyId });
    }

    public async Task InsertPriceAsync(AdministrationChargeCurrencyEntity price, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(InsertSql, BuildParameters(price));
    }

    public async Task UpdatePriceAsync(AdministrationChargeCurrencyEntity price, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        await connection.ExecuteAsync(UpdateSql, BuildParameters(price));
    }

    private const string InsertSql =
        "EXEC dbo.spiAdministrationChargeCurrency @AdministrationChargeCurrencyId, @AdministrationChargeId, @CurrencyId, @Price";

    private const string UpdateSql =
        "EXEC dbo.spuAdministrationChargeCurrency @AdministrationChargeCurrencyId, @AdministrationChargeId, @CurrencyId, @Price";

    private static DynamicParameters BuildParameters(AdministrationChargeCurrencyEntity price)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@AdministrationChargeCurrencyId", price.AdministrationChargeCurrencyId);
        parameters.Add("@AdministrationChargeId", price.AdministrationChargeId);
        parameters.Add("@CurrencyId", price.CurrencyId);
        parameters.Add("@Price", price.Price);
        return parameters;
    }
}
