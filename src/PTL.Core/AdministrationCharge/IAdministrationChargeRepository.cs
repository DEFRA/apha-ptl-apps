namespace PTL.Core.AdministrationCharge;

public interface IAdministrationChargeRepository
{
    // spgaAdministrationCharge - every charge name on record.
    Task<IReadOnlyList<AdministrationChargeEntity>> GetChargesAsync(CancellationToken cancellationToken = default);

    // spgaAdministrationChargeCurrency - every currency price row on record, for every charge.
    Task<IReadOnlyList<AdministrationChargeCurrencyEntity>> GetPricesAsync(CancellationToken cancellationToken = default);

    // spgAdministrationChargeCurrencyByAdministrationChargeIdCurrencyId - used by the service to
    // decide whether SetPriceAsync should insert or update.
    Task<AdministrationChargeCurrencyEntity?> GetPriceAsync(Guid administrationChargeId, Guid currencyId, CancellationToken cancellationToken = default);

    // spiAdministrationChargeCurrency
    Task InsertPriceAsync(AdministrationChargeCurrencyEntity price, CancellationToken cancellationToken = default);

    // spuAdministrationChargeCurrency
    Task UpdatePriceAsync(AdministrationChargeCurrencyEntity price, CancellationToken cancellationToken = default);
}
