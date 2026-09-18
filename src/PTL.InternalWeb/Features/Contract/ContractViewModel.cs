using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.Contracts.Contract;

namespace PTL.InternalWeb.Features.Contract;

// Search-criteria portion of the Index view, bound directly from the query string.
public sealed record ContractSearchViewModel(
    Guid CustomerId,
    int? YearId,
    ContractPeriodFilter Period,
    string? SearchTerm,
    int Page,
    int PageSize);

public sealed record ContractListViewModel(
    ContractSearchViewModel Search,
    int TotalCount,
    IReadOnlyList<ContractSummaryResponse> Contracts);

// Shared by Create.cshtml and Edit.cshtml. Validation attributes mirror
// PTL.Core.Contract.ContractValidator (see docs/analysis/contract-analysis.md, "Validation Rules")
// so invalid input is rejected client + server side before it ever reaches PTL.Api. The API's
// ContractValidator re-validates the same rules server-side as the authoritative source of truth.
public sealed class ContractFormViewModel : IValidatableObject
{
    public Guid? ContractId { get; set; }

    public Guid CustomerId { get; set; }

    // Read-only display fields, not posted back - mirrors legacy Contract.aspx never allowing
    // CustomerName/QalNumber/IsReadOnly/CommencementDate to be edited.
    public string? CustomerName { get; set; }

    public string? QalNumber { get; set; }

    public bool IsReadOnly { get; set; }

    public DateTime? CommencementDate { get; set; }

    [Required(ErrorMessage = "Enter a year.")]
    public int YearId { get; set; }

    // Populated by ContractController before the view is rendered - see /api/lookups/years
    // (current + next year only, mirrors legacy DropDownYear SetYearDropDown()).
    public IEnumerable<SelectListItem> YearOptions { get; set; } = [];

    [StringLength(10, ErrorMessage = "UT number must not exceed 10 characters.")]
    public string? UTNumber { get; set; }

    [StringLength(10, ErrorMessage = "FT number must not exceed 10 characters.")]
    public string? FTNumber { get; set; }

    [StringLength(50, ErrorMessage = "Contract signatory must not exceed 50 characters.")]
    public string? ContractSignatory { get; set; }

    [StringLength(1000, ErrorMessage = "Actions required must not exceed 1000 characters.")]
    public string? ActionsRequired { get; set; }

    [StringLength(2000, ErrorMessage = "Renewal information must not exceed 2000 characters.")]
    public string? RenewalInformation { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Discount rate must not be negative.")]
    public decimal DiscountRate { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Administration charge must not be negative.")]
    public decimal AdministrationCharge { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of courier deliveries must not be negative.")]
    public int NumberCourier { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Courier price must not be negative.")]
    public decimal CourierPrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of postage deliveries must not be negative.")]
    public int NumberPostage { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Postage price must not be negative.")]
    public decimal PostagePrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Number of special deliveries must not be negative.")]
    public int NumberSpecialDelivery { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Special delivery price must not be negative.")]
    public decimal SpecialDeliveryPrice { get; set; }

    [Required(ErrorMessage = "Enter the acknowledgement posted date.")]
    [DataType(DataType.Date)]
    public DateTime? AcknowledgementPostedDate { get; set; }

    [Required(ErrorMessage = "Enter the acknowledgement returned date.")]
    [DataType(DataType.Date)]
    public DateTime? AcknowledgementReturnedDate { get; set; }

    [Required(ErrorMessage = "Enter the job sheet posted date.")]
    [DataType(DataType.Date)]
    public DateTime? JobSheetPostedDate { get; set; }

    [StringLength(255, ErrorMessage = "Reason for closure must not exceed 255 characters.")]
    public string? ReasonForClosure { get; set; }

    [Required(ErrorMessage = "Enter the date of leaving.")]
    [DataType(DataType.Date)]
    public DateTime? DateOfLeaving { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(2, ErrorMessage = "Suffix must not exceed 2 characters.")]
    public string? Suffix { get; set; }

    [StringLength(255, ErrorMessage = "Purchase order number must not exceed 255 characters.")]
    public string? PurchaseOrderNumber { get; set; }

    public bool OptOutOfInvoiceGeneration { get; set; }

    public bool IsOnlineOrder { get; set; }

    // Preserves ValidateUTFT: exactly one of UTNumber/FTNumber must be populated.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasUt = !string.IsNullOrWhiteSpace(UTNumber);
        var hasFt = !string.IsNullOrWhiteSpace(FTNumber);
        if (hasUt == hasFt)
        {
            yield return new ValidationResult("Enter either a UT number or an FT number, but not both.", [nameof(UTNumber)]);
        }
    }
}
