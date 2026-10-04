namespace PTL.Core.AdministrationCharge;

// One row per (AdministrationCharge, Currency) price, backed by tlnkAdministrationChargeCurrency
// (mirrors PtaBusinessObjects.BusinessObjects.SystemObjects.AdministrationChargeCurrency). Currency
// name/symbol are resolved by the caller against ILookupService, same as ContractController does
// for CurrencySymbol - not duplicated here.
public class AdministrationChargeCurrencyEntity
{
    public Guid AdministrationChargeCurrencyId { get; set; }
    public Guid AdministrationChargeId { get; set; }
    public Guid CurrencyId { get; set; }
    public decimal Price { get; set; }
}
