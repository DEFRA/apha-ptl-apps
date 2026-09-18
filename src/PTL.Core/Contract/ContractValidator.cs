namespace PTL.Core.Contract;

// Preserves the validation rules from PtaBusinessObjects.BusinessObjects.Contracts.Contract.AddBusinessRules()
// (see docs/analysis/contract-analysis.md, "Validation Rules"). Field lengths/types match the
// spiContract/spuContract stored procedure parameters (verified against the live schema - 29
// parameters, no drift).
public static class ContractValidator
{
    // The legacy code-behind uses 1/1/9999 as a Csla.SmartDate "unset" sentinel and rejects it
    // server-side via AddBusinessRules' ValidateDate rule - preserved here verbatim.
    private static readonly DateTime UnsetDateSentinelYear = new(9999, 1, 1);

    public static ContractValidationResult Validate(Contract contract)
    {
        var errors = new List<ContractValidationError>();

        if (contract.CustomerId == Guid.Empty)
        {
            errors.Add(new ContractValidationError("CustomerId", "CustomerId cannot be an empty GUID."));
        }

        if (contract.YearId <= 0)
        {
            errors.Add(new ContractValidationError("YearId", "YearId is required."));
        }

        // ValidateUTFT: exactly one of UTNumber/FTNumber must be populated, never both, never neither.
        var hasUt = !string.IsNullOrWhiteSpace(contract.UTNumber);
        var hasFt = !string.IsNullOrWhiteSpace(contract.FTNumber);
        if (hasUt == hasFt)
        {
            errors.Add(new ContractValidationError("UTNumber", "Enter either a UT number or an FT number, but not both."));
        }

        MaxLength(contract.UTNumber, 10, "UTNumber", errors);
        MaxLength(contract.FTNumber, 10, "FTNumber", errors);
        MaxLength(contract.ContractSignatory, 50, "ContractSignatory", errors);
        MaxLength(contract.ActionsRequired, 1000, "ActionsRequired", errors);
        MaxLength(contract.RenewalInformation, 2000, "RenewalInformation", errors);
        MaxLength(contract.ReasonForClosure, 255, "ReasonForClosure", errors);
        MaxLength(contract.Suffix, 2, "Suffix", errors);
        MaxLength(contract.PurchaseOrderNumber, 255, "PurchaseOrderNumber", errors);

        MinValue(contract.DiscountRate, 0, "DiscountRate", errors);
        MinValue(contract.AdministrationCharge, 0, "AdministrationCharge", errors);
        MinValue(contract.NumberCourier, 0, "NumberCourier", errors);
        MinValue(contract.CourierPrice, 0, "CourierPrice", errors);
        MinValue(contract.NumberPostage, 0, "NumberPostage", errors);
        MinValue(contract.PostagePrice, 0, "PostagePrice", errors);
        MinValue(contract.NumberSpecialDelivery, 0, "NumberSpecialDelivery", errors);
        MinValue(contract.SpecialDeliveryPrice, 0, "SpecialDeliveryPrice", errors);

        ValidateNotSentinel(contract.AcknowledgementPostedDate, "AcknowledgementPostedDate", errors);
        ValidateNotSentinel(contract.AcknowledgementReturnedDate, "AcknowledgementReturnedDate", errors);
        ValidateNotSentinel(contract.JobSheetPostedDate, "JobSheetPostedDate", errors);
        ValidateNotSentinel(contract.DateOfLeaving, "DateOfLeaving", errors);

        return new ContractValidationResult(errors.Count == 0, errors);
    }

    private static void MaxLength(string value, int max, string field, List<ContractValidationError> errors)
    {
        if (value.Length > max)
        {
            errors.Add(new ContractValidationError(field, $"{field} must not exceed {max} characters."));
        }
    }

    private static void MinValue(decimal value, decimal min, string field, List<ContractValidationError> errors)
    {
        if (value < min)
        {
            errors.Add(new ContractValidationError(field, $"{field} must not be negative."));
        }
    }

    private static void MinValue(int value, int min, string field, List<ContractValidationError> errors)
    {
        if (value < min)
        {
            errors.Add(new ContractValidationError(field, $"{field} must not be negative."));
        }
    }

    private static void ValidateNotSentinel(DateTime? value, string field, List<ContractValidationError> errors)
    {
        // A true null (only possible when re-validating data loaded from a legacy row) is not
        // the sentinel and is left alone; only the explicit 1/1/9999 "unset" marker is rejected.
        if (value is { } dateValue && dateValue.Year == UnsetDateSentinelYear.Year)
        {
            errors.Add(new ContractValidationError(field, $"{field} must be a valid date."));
        }
    }
}
