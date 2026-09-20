namespace PTL.Core.Lookup;

// Keyless domain projection for the rows returned by spgaSchemeCurrency (all scheme-currency
// pricing links, filtered by SchemeId in SchemeService - see docs/analysis/scheme-analysis.md,
// "Scheme Currency Read Operations"). The legacy GetAllSchemeCurrency ASMX method is likewise
// unscoped by scheme - filtering is applied client-side here for a more useful read operation.
public class SchemeCurrencyEntity
{
    public Guid SchemeCurrencyId { get; set; }
    public Guid SchemeId { get; set; }
    public Guid CurrencyId { get; set; }
    public decimal Price { get; set; }
    public string CurrencyName { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = string.Empty;
}
