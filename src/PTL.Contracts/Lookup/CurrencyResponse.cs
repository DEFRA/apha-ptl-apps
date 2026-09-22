namespace PTL.Contracts.Lookup;

// Public API contract for GET /api/lookups/currencies; matches the legacy DropDownCurrency binding
// (SystemObjects.CurrencyCollection / spgaCurrency, DataTextField "LongName", DataValueField "CurrencyId").
// LongName ("£ - British Pound") is provided directly so callers don't need to recompute it.
public sealed record CurrencyResponse(Guid CurrencyId, string Name, string Symbol, string LongName);
