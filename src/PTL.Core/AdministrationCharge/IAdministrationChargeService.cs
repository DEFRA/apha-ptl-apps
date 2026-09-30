namespace PTL.Core.AdministrationCharge;

public interface IAdministrationChargeService
{
    Task<IReadOnlyList<AdministrationChargeEntity>> GetChargesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdministrationChargeCurrencyEntity>> GetPricesAsync(CancellationToken cancellationToken = default);

    // Inserts a new price row if one doesn't yet exist for this (charge, currency) pair, otherwise
    // updates it in place - mirrors the legacy AdministrationChargeCurrency.aspx.vb
    // TextboxTextChanged handler.
    Task<AdministrationChargeValidationResult> SetPriceAsync(Guid administrationChargeId, Guid currencyId, decimal price, CancellationToken cancellationToken = default);
}
