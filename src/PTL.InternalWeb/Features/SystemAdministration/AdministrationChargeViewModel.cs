using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.SystemAdministration;

// One row per administration charge, one cell per system currency (in ILookupApiClient.
// GetCurrenciesAsync order) - mirrors the legacy AdministrationChargeCurrency.aspx grid (a fixed
// "Charge Name" column plus one dynamically generated column per currency).
public sealed class AdministrationChargeListViewModel
{
    public List<AdministrationChargeRowViewModel> Charges { get; set; } = [];
}

public sealed class AdministrationChargeRowViewModel
{
    public Guid AdministrationChargeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<AdministrationChargePriceCellViewModel> Prices { get; set; } = [];
}

public sealed class AdministrationChargePriceCellViewModel
{
    public Guid CurrencyId { get; set; }

    // e.g. "£ - British Pound" - used as the column header and the price input's label. Not
    // resolvable from the posted form alone, so it round-trips via a hidden field.
    public string CurrencyLabel { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Price must not be negative")]
    public decimal Price { get; set; }
}
