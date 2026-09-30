namespace PTL.Contracts.AdministrationCharge;

// Public API contract for PUT /api/administration-charges/price.
public sealed record UpdateAdministrationChargePriceRequest(Guid AdministrationChargeId, Guid CurrencyId, decimal Price);
