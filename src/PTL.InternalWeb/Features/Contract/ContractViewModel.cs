using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.Contracts.Contract;
using PTL.Contracts.Customer;

namespace PTL.InternalWeb.Features.Contract;

// Search-criteria portion of the Index view, bound directly from the query string.
public sealed record ContractSearchViewModel(
    Guid CustomerId,
    int? YearId,
    ContractPeriodFilter Period,
    string? SearchTerm,
    int Page,
    int PageSize);

// Customer and YearNames mirror legacy ContractList.aspx.vb: LblSubTitle.Text (QAL/Name/Organisation
// header) and GetYearNameFromYearId (YearId -> "2025/26" display text via a separately fetched
// year list) - Customer is null only if the lookup failed, in which case the header is omitted.
public sealed record ContractListViewModel(
    ContractSearchViewModel Search,
    int TotalCount,
    IReadOnlyList<ContractSummaryResponse> Contracts,
    CustomerResponse? Customer,
    IReadOnlyDictionary<int, string> YearNames);

// Shared by Create.cshtml and Edit.cshtml. Validation attributes mirror
// PTL.Core.Contract.ContractValidator (see docs/analysis/contract-analysis.md, "Validation Rules")
// so invalid input is rejected client + server side before it ever reaches PTL.Api. The API's
// ContractValidator re-validates the same rules server-side as the authoritative source of truth.
public sealed partial class ContractFormViewModel : IValidatableObject
{
    public Guid? ContractId { get; set; }

    public Guid? CustomerId { get; set; }

    // Read-only display fields, not posted back - mirrors legacy Contract.aspx never allowing
    // CustomerName/QalNumber/IsReadOnly/CommencementDate to be edited.
    public string? CustomerName { get; set; }

    public string? QalNumber { get; set; }

    public bool? IsReadOnly { get; set; }

    public DateTime? CommencementDate { get; set; }

    // Read-only display fields, system-managed - never posted back. IsInvoiceSent mirrors legacy
    // Contract.aspx's CheckBoxIsInvoiceSent (permanently Enabled="false"); CurrencySymbol is
    // resolved from the owning customer's currency by ContractController (see
    // Contract.aspx.vb LoadLabelNames() using mCurrency.Symbol for the pricing field labels).
    public bool IsInvoiceSent { get; set; }

    public string CurrencySymbol { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a year")]
    [Display(Name = "Year")]
    public int? YearId { get; set; }

    // Populated by ContractController before the view is rendered - see /api/lookups/years
    // (current + next year only, mirrors legacy DropDownYear SetYearDropDown()).
    public IEnumerable<SelectListItem> YearOptions { get; set; } = [];

    [StringLength(10, ErrorMessage = "UT number must not exceed 10 characters")]
    public string? UTNumber { get; set; }

    [StringLength(10, ErrorMessage = "FT number must not exceed 10 characters")]
    public string? FTNumber { get; set; }

    // UI-only concept (Contract.aspx.vb DropDownUTFT), posted back as a real field so Validate()
    // knows which single number the user actually intends to enter - not part of ContractRequest.
    public string? ContractType { get; set; }

    [StringLength(50, ErrorMessage = "Contract signatory must not exceed 50 characters")]
    public string? ContractSignatory { get; set; }

    [StringLength(1000, ErrorMessage = "Actions required must not exceed 1000 characters")]
    public string? ActionsRequired { get; set; }

    [StringLength(2000, ErrorMessage = "Renewal information must not exceed 2000 characters")]
    public string? RenewalInformation { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Discount rate must not be negative")]
    [Display(Name = "Discount rate")]
    public decimal? DiscountRate { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Administration charge must not be negative")]
    [Display(Name = "Administration charge")]
    public decimal? AdministrationCharge { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of courier deliveries must not be negative")]
    [Display(Name = "Number of courier items")]
    public int? NumberCourier { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Courier price must not be negative")]
    [Display(Name = "Courier price per item")]
    public decimal? CourierPrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of postage deliveries must not be negative")]
    [Display(Name = "Number of postage items")]
    public int? NumberPostage { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Postage price must not be negative")]
    [Display(Name = "Postage price per item")]
    public decimal? PostagePrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of special deliveries must not be negative")]
    [Display(Name = "Number of special delivery items")]
    public int? NumberSpecialDelivery { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Special delivery price must not be negative")]
    [Display(Name = "Special delivery price per item")]
    public decimal? SpecialDeliveryPrice { get; set; }

    // Not [Required] - legacy Contract.aspx has no RequiredFieldValidator on these dates, only a
    // dd/mm/yyyy RegularExpressionValidator; a blank value is left as null, matching
    // ContractValidator's ValidateNotSentinel (only the 1/1/9999 sentinel is rejected).
    [DataType(DataType.Date)]
    public DateTime? AcknowledgementPostedDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? AcknowledgementReturnedDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? JobSheetPostedDate { get; set; }

    [StringLength(255, ErrorMessage = "Reason for closure must not exceed 255 characters")]
    public string? ReasonForClosure { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DateOfLeaving { get; set; }

    // S6964 (value-type controller-action input) suppressed for this block: these are HTML
    // checkboxes, where an unchecked box simply isn't posted and the framework's own
    // asp-for-generated hidden companion input already supplies "false" - non-nullable bool
    // defaulting to false on under-posting is the framework-intended behaviour, not a bug.
#pragma warning disable S6964
    public bool IsActive { get; set; } = true;

    [StringLength(2, ErrorMessage = "Suffix must not exceed 2 characters")]
    public string? Suffix { get; set; }

    [StringLength(255, ErrorMessage = "Purchase order number must not exceed 255 characters")]
    public string? PurchaseOrderNumber { get; set; }

    public bool OptOutOfInvoiceGeneration { get; set; }

    public bool IsOnlineOrder { get; set; }
#pragma warning restore S6964

    // Preserves ValidateUTFT: exactly one of UTNumber/FTNumber must be populated, in the legacy
    // format (Contract.aspx.vb SetUT()/SetFT() RegularExpressionValidator expressions). Compiled
    // at build time (GeneratedRegexAttribute) with a timeout so a pathological input can't hang
    // the request thread.
    [GeneratedRegex("^UT[0-9]/[0-9]{1,3}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UtNumberFormat();

    [GeneratedRegex("^[0-9]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FtNumberFormat();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasUt = !string.IsNullOrWhiteSpace(UTNumber);
        var hasFt = !string.IsNullOrWhiteSpace(FTNumber);
        var isUtSelected = !string.Equals(ContractType, "FT", StringComparison.OrdinalIgnoreCase);

        if (!hasUt && !hasFt)
        {
            yield return isUtSelected
                ? new ValidationResult("Enter a UT number", [nameof(UTNumber)])
                : new ValidationResult("Enter an FT number", [nameof(FTNumber)]);
        }
        else if (hasUt && hasFt)
        {
            yield return new ValidationResult("Enter either a UT number or an FT number, but not both", [nameof(UTNumber)]);
        }
        else if (hasUt && !UtNumberFormat().IsMatch(UTNumber!))
        {
            yield return new ValidationResult("Enter a valid UT number, for example UT3/306", [nameof(UTNumber)]);
        }
        else if (hasFt && !FtNumberFormat().IsMatch(FTNumber!))
        {
            yield return new ValidationResult("Enter a valid FT number, for example 1000", [nameof(FTNumber)]);
        }
    }
}
