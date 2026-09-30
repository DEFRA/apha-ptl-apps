using PTL.Core.AdministrationCharge;

namespace PTL.Api.Tests.AdministrationCharge;

// In-memory IAdministrationChargeRepository test double so AdministrationChargeService can be
// tested without a real database or the spga/spg/spi/spu AdministrationCharge* stored procedures.
internal sealed class FakeAdministrationChargeRepository : IAdministrationChargeRepository
{
    public IReadOnlyList<AdministrationChargeEntity> Charges { get; set; } = [];
    public List<AdministrationChargeCurrencyEntity> Prices { get; set; } = [];
    public List<AdministrationChargeCurrencyEntity> Inserted { get; } = [];
    public List<AdministrationChargeCurrencyEntity> Updated { get; } = [];

    public Task<IReadOnlyList<AdministrationChargeEntity>> GetChargesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Charges);

    public Task<IReadOnlyList<AdministrationChargeCurrencyEntity>> GetPricesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AdministrationChargeCurrencyEntity>>(Prices);

    public Task<AdministrationChargeCurrencyEntity?> GetPriceAsync(Guid administrationChargeId, Guid currencyId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Prices.SingleOrDefault(p => p.AdministrationChargeId == administrationChargeId && p.CurrencyId == currencyId));

    public Task InsertPriceAsync(AdministrationChargeCurrencyEntity price, CancellationToken cancellationToken = default)
    {
        Inserted.Add(price);
        Prices.Add(price);
        return Task.CompletedTask;
    }

    public Task UpdatePriceAsync(AdministrationChargeCurrencyEntity price, CancellationToken cancellationToken = default)
    {
        Updated.Add(price);
        return Task.CompletedTask;
    }
}
