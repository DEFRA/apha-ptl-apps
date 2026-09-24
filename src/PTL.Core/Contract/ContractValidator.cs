using System.Text.RegularExpressions;

namespace PTL.Core.Contract;

// Preserves the validation rules from PtaBusinessObjects.BusinessObjects.Contracts.Contract.AddBusinessRules()
// (see docs/analysis/contract-analysis.md, "Validation Rules"). Field lengths/types match the
// spiContract/spuContract stored procedure parameters (verified against the live schema - 29
// parameters, no drift).
public static partial class ContractValidator
{
    // The legacy code-behind uses 1/1/9999 as a Csla.SmartDate "unset" sentinel and rejects it
    // server-side via AddBusinessRules' ValidateDate rule - preserved here verbatim.
    private static readonly DateTime UnsetDateSentinelYear = new(9999, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    // Preserved from Contract.aspx.vb's SetUT()/SetFT() RegularExpressionValidator expressions -
    // e.g. a valid UT number looks like "UT3/306", a valid FT number looks like "1000".
    [GeneratedRegex("^UT[0-9]/[0-9]{1,3}$")]
    private static partial Regex UtNumberFormat();

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex FtNumberFormat();

    private const string UtNumberField = "UTNumber";

    // Human-readable labels for error messages - mirrors CustomerValidator's Label() pattern so
    // messages read e.g. "UT number must not exceed 10 characters." instead of "UTNumber must...".
    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        ["CustomerId"] = "Customer",
        ["YearId"] = "Year",
        [UtNumberField] = "UT number",
        ["FTNumber"] = "FT number",
        ["ContractSignatory"] = "Contract signatory",
        ["ActionsRequired"] = "Actions required",
        ["RenewalInformation"] = "Renewal information",
        ["ReasonForClosure"] = "Cancellation reason",
        ["Suffix"] = "Suffix",
        ["PurchaseOrderNumber"] = "Purchase order number",
        ["DiscountRate"] = "Discount rate",
        ["AdministrationCharge"] = "Administration charge",
        ["NumberCourier"] = "Number of courier items",
        ["CourierPrice"] = "Courier price per item",
        ["NumberPostage"] = "Number of postage items",
        ["PostagePrice"] = "Postage price per item",
        ["NumberSpecialDelivery"] = "Number of special delivery items",
        ["SpecialDeliveryPrice"] = "Special delivery price per item",
        ["AcknowledgementPostedDate"] = "Acknowledgement posted date",
        ["AcknowledgementReturnedDate"] = "Acknowledgement returned date",
        ["JobSheetPostedDate"] = "Job sheet sent date",
        ["DateOfLeaving"] = "Cancellation date",
    };

    private static string Label(string field) => FieldLabels.GetValueOrDefault(field, field);

    public static ContractValidationResult Validate(Contract contract)
    {
        var errors = new List<ContractValidationError>();

        if (contract.CustomerId == Guid.Empty)
        {
            errors.Add(new ContractValidationError("CustomerId", "Select a customer"));
        }

        if (contract.YearId <= 0)
        {
            errors.Add(new ContractValidationError("YearId", "Select a year"));
        }

        // ValidateUTFT: exactly one of UTNumber/FTNumber must be populated, never both, never neither.
        var hasUt = !string.IsNullOrWhiteSpace(contract.UTNumber);
        var hasFt = !string.IsNullOrWhiteSpace(contract.FTNumber);
        if (!hasUt && !hasFt)
        {
            errors.Add(new ContractValidationError(UtNumberField, "Enter a UT number or an FT number"));
        }
        else if (hasUt && hasFt)
        {
            errors.Add(new ContractValidationError(UtNumberField, "Enter either a UT number or an FT number, but not both"));
        }
        else if (hasUt && !UtNumberFormat().IsMatch(contract.UTNumber))
        {
            errors.Add(new ContractValidationError(UtNumberField, "Enter a valid UT number, for example UT3/306"));
        }
        else if (hasFt && !FtNumberFormat().IsMatch(contract.FTNumber))
        {
            errors.Add(new ContractValidationError("FTNumber", "Enter a valid FT number, for example 1000"));
        }

        MaxLength(contract.UTNumber, 10, UtNumberField, errors);
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
            errors.Add(new ContractValidationError(field, $"{Label(field)} must not exceed {max} characters"));
        }
    }

    private static void MinValue(decimal value, decimal min, string field, List<ContractValidationError> errors)
    {
        if (value < min)
        {
            errors.Add(new ContractValidationError(field, $"{Label(field)} must not be negative"));
        }
    }

    private static void MinValue(int value, int min, string field, List<ContractValidationError> errors)
    {
        if (value < min)
        {
            errors.Add(new ContractValidationError(field, $"{Label(field)} must not be negative"));
        }
    }

    private static void ValidateNotSentinel(DateTime? value, string field, List<ContractValidationError> errors)
    {
        // A true null (only possible when re-validating data loaded from a legacy row) is not
        // the sentinel and is left alone; only the explicit 1/1/9999 "unset" marker is rejected.
        if (value is { } dateValue && dateValue.Year == UnsetDateSentinelYear.Year)
        {
            errors.Add(new ContractValidationError(field, $"{Label(field)} must be a valid date"));
        }
    }
}
