namespace PTL.Core.AdministrationCharge;

// Preserves the single validation rule from PtaBusinessObjects.BusinessObjects.SystemObjects.
// AdministrationChargeCurrency.AddBusinessRules(): MinValue(Price, 0).
public static class AdministrationChargeValidator
{
    private static readonly AdministrationChargeValidationResult Valid = new(true, []);

    public static AdministrationChargeValidationResult ValidatePrice(decimal price) =>
        price < 0
            ? new AdministrationChargeValidationResult(false, [new AdministrationChargeValidationError("Price", "Price must not be negative")])
            : Valid;
}
