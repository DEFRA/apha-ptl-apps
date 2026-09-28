namespace PTL.Core.AdministrationCharge;

public sealed class AdministrationChargeService(IAdministrationChargeRepository repository) : IAdministrationChargeService
{
    public Task<IReadOnlyList<AdministrationChargeEntity>> GetChargesAsync(CancellationToken cancellationToken = default) =>
        repository.GetChargesAsync(cancellationToken);

    public Task<IReadOnlyList<AdministrationChargeCurrencyEntity>> GetPricesAsync(CancellationToken cancellationToken = default) =>
        repository.GetPricesAsync(cancellationToken);

    public async Task<AdministrationChargeValidationResult> SetPriceAsync(Guid administrationChargeId, Guid currencyId, decimal price, CancellationToken cancellationToken = default)
    {
        var validation = AdministrationChargeValidator.ValidatePrice(price);
        if (!validation.IsValid)
        {
            return validation;
        }

        var existing = await repository.GetPriceAsync(administrationChargeId, currencyId, cancellationToken);
        if (existing is null)
        {
            await repository.InsertPriceAsync(new AdministrationChargeCurrencyEntity
            {
                AdministrationChargeCurrencyId = Guid.NewGuid(),
                AdministrationChargeId = administrationChargeId,
                CurrencyId = currencyId,
                Price = price
            }, cancellationToken);
        }
        else
        {
            existing.Price = price;
            await repository.UpdatePriceAsync(existing, cancellationToken);
        }

        return validation;
    }
}
