namespace PTL.Contracts.Lookup;

// Public API contract for scheme currency pricing reads (spgaSchemeCurrency, filtered by
// SchemeId - see docs/analysis/scheme-analysis.md, "Scheme Currency Read Operations").
public sealed record SchemeCurrencyResponse(Guid SchemeCurrencyId, Guid SchemeId, Guid CurrencyId, decimal Price, string CurrencyName, string CurrencySymbol);
