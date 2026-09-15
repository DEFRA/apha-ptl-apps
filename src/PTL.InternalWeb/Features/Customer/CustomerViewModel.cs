using System.ComponentModel.DataAnnotations;
using PTL.Contracts.Customer;
using PTL.Core.Customer;
using CoreCustomer = PTL.Core.Customer.Customer;

namespace PTL.InternalWeb.Features.Customer;

public sealed record CustomerListViewModel(
    string? SearchTerm,
    CustomerStatusFilter SelectedStatus,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<CustomerSummaryResponse> Customers);

// Confirmation screen for the deactivation workflow (Details.cshtml -> Deactivate.cshtml -> POST).
// CustomerStatusId records the inactive reason, mirroring the legacy inactive-error flag.
public sealed class DeactivateCustomerViewModel
{
    public Guid CustomerId { get; set; }

    public string Name { get; set; } = string.Empty;

    // [NEEDS INVESTIGATION] rendered as a raw GUID pending a CustomerStatus lookup API/stored procedure.
    [Required(ErrorMessage = "Select a customer status.")]
    public Guid? CustomerStatusId { get; set; }
}


// Shared by Create.cshtml and Edit.cshtml. Validation attributes mirror
// PtaBusinessObjects.BusinessObjects.Contracts.Customer.AddBusinessRules() (see
// docs/analysis/customer-analysis.md) so invalid input is rejected client + server side before
// it ever reaches PTL.Api. PTL.Core.Customer.CustomerValidator re-validates the same rules
// server-side as the authoritative source of truth.
public sealed class CustomerFormViewModel : IValidatableObject
{
    public Guid? CustomerId { get; set; }

    public string? QalNumber { get; set; }

    [Required(ErrorMessage = "Enter a name.")]
    [StringLength(50, ErrorMessage = "Name must not exceed 50 characters.")]
    public string Name { get; set; } = string.Empty;

    // These fields are genuinely optional in the legacy Customer.aspx form (see
    // CustomerValidator in PTL.Core, which only requires a subset while IsActive is true).
    // They must stay nullable: with <Nullable>enable</Nullable>, ASP.NET Core MVC implicitly
    // treats a non-nullable `string` as [Required] and rejects an empty submitted value.
    [StringLength(50, ErrorMessage = "Previous name must not exceed 50 characters.")]
    public string? PreviousName { get; set; }

    [StringLength(10, ErrorMessage = "Registered file number must not exceed 10 characters.")]
    [RegularExpression(@"^(QAL/[0-9]*)?$", ErrorMessage = "Registered file number must match the format QAL/nnnnn.")]
    public string? RegisteredFileNumber { get; set; }

    // [NEEDS INVESTIGATION] rendered as a raw GUID pending a CustomerType lookup API/stored procedure.
    [Required(ErrorMessage = "Enter a customer type ID.")]
    public Guid CustomerTypeId { get; set; }

    [StringLength(20, ErrorMessage = "VAT number must not exceed 20 characters.")]
    public string? VatNumber { get; set; }

    // [NEEDS INVESTIGATION] rendered as a raw GUID pending a VatRating lookup API/stored procedure.
    public Guid VatRatingId { get; set; }

    [StringLength(20, ErrorMessage = "Account number must not exceed 20 characters.")]
    public string? AccountNumber { get; set; }

    [StringLength(30, ErrorMessage = "Customer finance ID must not exceed 30 characters.")]
    public string? CustomerFinanceId { get; set; }

    [StringLength(50, ErrorMessage = "Contact name must not exceed 50 characters.")]
    public string? ContactName { get; set; }

    [StringLength(50, ErrorMessage = "Organisation must not exceed 50 characters.")]
    public string? Organisation { get; set; }

    [StringLength(100, ErrorMessage = "Address line 1 must not exceed 100 characters.")]
    public string? Address1 { get; set; }

    [StringLength(100, ErrorMessage = "Address line 2 must not exceed 100 characters.")]
    public string? Address2 { get; set; }

    [StringLength(100, ErrorMessage = "Address line 3 must not exceed 100 characters.")]
    public string? Address3 { get; set; }

    [StringLength(100, ErrorMessage = "Address line 4 must not exceed 100 characters.")]
    public string? Address4 { get; set; }

    [StringLength(100, ErrorMessage = "Address line 5 must not exceed 100 characters.")]
    public string? Address5 { get; set; }

    // [NEEDS INVESTIGATION] rendered as a raw GUID pending a Country lookup API/stored procedure.
    public Guid CountryId { get; set; }

    [StringLength(20, ErrorMessage = "Telephone must not exceed 20 characters.")]
    [RegularExpression(@"^[ 0-9\+\-\(\)\*\#]*$", ErrorMessage = "Telephone contains characters that are not allowed.")]
    public string? Telephone { get; set; }

    [StringLength(20, ErrorMessage = "Telephone (alternative) must not exceed 20 characters.")]
    [RegularExpression(@"^[ 0-9\+\-\(\)\*\#]*$", ErrorMessage = "Telephone (alternative) contains characters that are not allowed.")]
    public string? Telephone2 { get; set; }

    [StringLength(20, ErrorMessage = "Fax must not exceed 20 characters.")]
    [RegularExpression(@"^[ 0-9\+\-\(\)\*\#]*$", ErrorMessage = "Fax contains characters that are not allowed.")]
    public string? Fax { get; set; }

    [StringLength(150, ErrorMessage = "Email must not exceed 150 characters.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? Email { get; set; }

    // [NEEDS INVESTIGATION] rendered as a raw GUID pending a Currency lookup API/stored procedure.
    public Guid CurrencyId { get; set; }

    [StringLength(2000, ErrorMessage = "Comments must not exceed 2000 characters.")]
    public string? Comments { get; set; }

    [StringLength(500, ErrorMessage = "Postage arrangements must not exceed 500 characters.")]
    public string? PostageArrangements { get; set; }

    public bool PaymentNonUK { get; set; }

    [StringLength(50, ErrorMessage = "Invoice name must not exceed 50 characters.")]
    public string? InvoiceName { get; set; }

    [StringLength(50, ErrorMessage = "Invoice organisation must not exceed 50 characters.")]
    public string? InvoiceOrganisation { get; set; }

    [StringLength(100, ErrorMessage = "Invoice address line 1 must not exceed 100 characters.")]
    public string? InvoiceAddress1 { get; set; }

    [StringLength(100, ErrorMessage = "Invoice address line 2 must not exceed 100 characters.")]
    public string? InvoiceAddress2 { get; set; }

    [StringLength(100, ErrorMessage = "Invoice address line 3 must not exceed 100 characters.")]
    public string? InvoiceAddress3 { get; set; }

    [StringLength(100, ErrorMessage = "Invoice address line 4 must not exceed 100 characters.")]
    public string? InvoiceAddress4 { get; set; }

    [StringLength(100, ErrorMessage = "Invoice address line 5 must not exceed 100 characters.")]
    public string? InvoiceAddress5 { get; set; }

    // [NEEDS INVESTIGATION] rendered as a raw GUID pending a Country lookup API/stored procedure.
    public Guid InvoiceCountryId { get; set; }

    [StringLength(20, ErrorMessage = "Invoice telephone must not exceed 20 characters.")]
    [RegularExpression(@"^[ 0-9\+\-\(\)\*\#]*$", ErrorMessage = "Invoice telephone contains characters that are not allowed.")]
    public string? InvoiceTelephone { get; set; }

    [StringLength(20, ErrorMessage = "Invoice telephone (alternative) must not exceed 20 characters.")]
    [RegularExpression(@"^[ 0-9\+\-\(\)\*\#]*$", ErrorMessage = "Invoice telephone (alternative) contains characters that are not allowed.")]
    public string? InvoiceTelephone2 { get; set; }

    [StringLength(20, ErrorMessage = "Invoice fax must not exceed 20 characters.")]
    [RegularExpression(@"^[ 0-9\+\-\(\)\*\#]*$", ErrorMessage = "Invoice fax contains characters that are not allowed.")]
    public string? InvoiceFax { get; set; }

    [StringLength(150, ErrorMessage = "Invoice email must not exceed 150 characters.")]
    [EmailAddress(ErrorMessage = "Enter a valid invoice email address.")]
    public string? InvoiceEmail { get; set; }

    public bool IsActive { get; set; } = true;

    public bool CanOrderOnline { get; set; }

    // [NEEDS INVESTIGATION] rendered as a raw GUID pending a CustomerStatus lookup API/stored
    // procedure; only meaningful while IsActive is false (mirrors the legacy "inactive error" flag).
    public Guid? CustomerStatusId { get; set; }

    // Runs PTL.Core's CustomerValidator (the same rules PTL.Api enforces - not duplicated) as part
    // of the normal MVC ModelState validation pass, so conditional-required-when-active fields and
    // the CustomerTypeId-not-empty rule are caught on the same submit as the DataAnnotations above,
    // instead of only surfacing after a round trip to the API.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var customer = new CoreCustomer
        {
            Name = Name,
            PreviousName = PreviousName ?? string.Empty,
            RegisteredFileNumber = RegisteredFileNumber ?? string.Empty,
            CustomerTypeId = CustomerTypeId,
            VatNumber = VatNumber ?? string.Empty,
            VatRatingId = VatRatingId,
            AccountNumber = AccountNumber ?? string.Empty,
            CustomerFinanceId = CustomerFinanceId ?? string.Empty,
            ContactName = ContactName ?? string.Empty,
            Organisation = Organisation ?? string.Empty,
            Address1 = Address1 ?? string.Empty,
            Address2 = Address2 ?? string.Empty,
            Address3 = Address3 ?? string.Empty,
            Address4 = Address4 ?? string.Empty,
            Address5 = Address5 ?? string.Empty,
            CountryId = CountryId,
            Telephone = Telephone ?? string.Empty,
            Telephone2 = Telephone2 ?? string.Empty,
            Fax = Fax ?? string.Empty,
            Email = Email ?? string.Empty,
            CurrencyId = CurrencyId,
            Comments = Comments ?? string.Empty,
            PostageArrangements = PostageArrangements ?? string.Empty,
            PaymentNonUK = PaymentNonUK,
            InvoiceName = InvoiceName ?? string.Empty,
            InvoiceOrganisation = InvoiceOrganisation ?? string.Empty,
            InvoiceAddress1 = InvoiceAddress1 ?? string.Empty,
            InvoiceAddress2 = InvoiceAddress2 ?? string.Empty,
            InvoiceAddress3 = InvoiceAddress3 ?? string.Empty,
            InvoiceAddress4 = InvoiceAddress4 ?? string.Empty,
            InvoiceAddress5 = InvoiceAddress5 ?? string.Empty,
            InvoiceCountryId = InvoiceCountryId,
            InvoiceTelephone = InvoiceTelephone ?? string.Empty,
            InvoiceTelephone2 = InvoiceTelephone2 ?? string.Empty,
            InvoiceFax = InvoiceFax ?? string.Empty,
            InvoiceEmail = InvoiceEmail ?? string.Empty,
            IsActive = IsActive,
            CanOrderOnline = CanOrderOnline,
            CustomerStatusId = CustomerStatusId
        };

        var result = CustomerValidator.Validate(customer);
        foreach (var error in result.Errors)
        {
            yield return new ValidationResult(error.Message, [error.Field]);
        }
    }
}
