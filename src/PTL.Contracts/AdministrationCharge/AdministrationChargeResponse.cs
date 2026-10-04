namespace PTL.Contracts.AdministrationCharge;

// Public API contract for GET /api/administration-charges - one row per charge, with every
// currency's price nested inside (see PTL.Core.AdministrationCharge.AdministrationChargeService).
// CurrencySymbol/CurrencyName are resolved by the caller against /api/lookups/currencies, the same
// join ContractController already does for CurrencySymbol - not duplicated server-side here.
public sealed record AdministrationChargeResponse(Guid AdministrationChargeId, string Name, IReadOnlyList<AdministrationChargeCurrencyPriceResponse> Prices);

public sealed record AdministrationChargeCurrencyPriceResponse(Guid CurrencyId, decimal Price);
