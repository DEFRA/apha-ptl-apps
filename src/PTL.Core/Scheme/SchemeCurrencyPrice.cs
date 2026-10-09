namespace PTL.Core.Scheme;

// One row of the legacy Details tab's currency pricing grid (tlnkSchemeCurrency), persisted by
// spiSchemeCurrency / spuSchemeCurrency. Legacy has no spdSchemeCurrency, so rows are only ever
// inserted or updated - a currency link is never removed.
public sealed class SchemeCurrencyPrice
{
    public Guid SchemeCurrencyId { get; set; }
    public Guid CurrencyId { get; set; }
    public decimal Price { get; set; }
    public string CurrencyName { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = string.Empty;
}
