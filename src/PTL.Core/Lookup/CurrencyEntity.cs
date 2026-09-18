namespace PTL.Core.Lookup;

// Keyless domain projection for the list returned by spgaCurrency, ordered by fldOrder.
// LongName (e.g. "£ - British Pound") mirrors PtaBusinessObjects.BusinessObjects.SystemObjects.Currency.LongName.
public class CurrencyEntity
{
    public Guid CurrencyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;

    public string LongName => $"{Symbol} - {Name}";
}
