using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
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


// Shared by Create.cshtml and Edit.cshtml. Validation attributes mirror
// PtaBusinessObjects.BusinessObjects.Contracts.Customer.AddBusinessRules() (see
// docs/analysis/customer-analysis.md) so invalid input is rejected client + server side before
// it ever reaches PTL.Api. PTL.Core.Customer.CustomerValidator re-validates the same rules
// server-side as the authoritative source of truth.
public sealed class CustomerFormViewModel : IValidatableObject
{
    public Guid? CustomerId { get; set; }

    public string? QalNumber { get; set; }

    // Display-only - server-generated (CustomerService.CreateCustomerAsync sets it to
    // DateTime.UtcNow and UpdateCustomerAsync always preserves the existing value), never posted
    // back or trusted from the client. Populated by CustomerController with a "today" preview on
    // Create and the persisted value on Edit, matching legacy Customer.aspx's
    // TextboxInitialStartDate.Enabled = False.
    public DateTime? InitialStartDate { get; set; }

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

    [Required(ErrorMessage = "Select a customer type.")]
    public Guid? CustomerTypeId { get; set; }

    // Populated by CustomerController before the view is rendered (GET, and re-populated on a
    // failed POST) from ILookupApiClient.GetCustomerTypesAsync - see /api/lookups/customer-types.
    public IEnumerable<SelectListItem> CustomerTypeOptions { get; set; } = [];

    [StringLength(20, ErrorMessage = "VAT number must not exceed 20 characters.")]
    public string? VatNumber { get; set; }

    // [NEEDS INVESTIGATION] rendered as a raw GUID pending a VatRating lookup API/stored procedure.
    public Guid? VatRatingId { get; set; }

    // Populated by CustomerController before the view is rendered - see /api/lookups/vat-ratings.
    public IEnumerable<SelectListItem> VatRatingOptions { get; set; } = [];

    [StringLength(20, ErrorMessage = "Account number must not exceed 20 characters.")]
    public string? AccountNumber { get; set; }

    [StringLength(30, ErrorMessage = "Customer ID must not exceed 30 characters.")]
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

    public Guid? CountryId { get; set; }

    // Populated by CustomerController before the view is rendered - see /api/lookups/countries.
    public IEnumerable<SelectListItem> CountryOptions { get; set; } = [];

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

    public Guid? CurrencyId { get; set; }

    // Populated by CustomerController before the view is rendered - see /api/lookups/currencies.
    public IEnumerable<SelectListItem> CurrencyOptions { get; set; } = [];

    [StringLength(2000, ErrorMessage = "Comments must not exceed 2000 characters.")]
    public string? Comments { get; set; }

    [StringLength(500, ErrorMessage = "Postage arrangements must not exceed 500 characters.")]
    public string? PostageArrangements { get; set; }

    // S6964 (value-type controller-action input) suppressed for this block: these are HTML
    // checkboxes, where an unchecked box simply isn't posted and the framework's own
    // asp-for-generated hidden companion input already supplies "false" - non-nullable bool
    // defaulting to false on under-posting is the framework-intended behaviour, not a bug.
#pragma warning disable S6964
    public bool PaymentNonUK { get; set; }
#pragma warning restore S6964

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

    public Guid? InvoiceCountryId { get; set; }

    // Populated by CustomerController before the view is rendered - see /api/lookups/countries
    // (same list as CountryOptions, rendered as a second dropdown - mirrors DropDownInvoiceCountry).
    public IEnumerable<SelectListItem> InvoiceCountryOptions { get; set; } = [];

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

#pragma warning disable S6964
    public bool IsActive { get; set; } = true;

    public bool CanOrderOnline { get; set; }
#pragma warning restore S6964

    // Display-only - server-generated by CustomerService.ApplyStatusTransition (set on deactivate,
    // cleared on reactivate), never posted back or trusted from the client. Rendered greyed out
    // and only shown once IsActive is unchecked, matching the "Inactive from" legacy concept.
    public DateTime? InactiveDate { get; set; }

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
            CustomerTypeId = CustomerTypeId.GetValueOrDefault(),
            VatNumber = VatNumber ?? string.Empty,
            VatRatingId = VatRatingId.GetValueOrDefault(),
            AccountNumber = AccountNumber ?? string.Empty,
            CustomerFinanceId = CustomerFinanceId ?? string.Empty,
            ContactName = ContactName ?? string.Empty,
            Organisation = Organisation ?? string.Empty,
            Address1 = Address1 ?? string.Empty,
            Address2 = Address2 ?? string.Empty,
            Address3 = Address3 ?? string.Empty,
            Address4 = Address4 ?? string.Empty,
            Address5 = Address5 ?? string.Empty,
            CountryId = CountryId.GetValueOrDefault(),
            Telephone = Telephone ?? string.Empty,
            Telephone2 = Telephone2 ?? string.Empty,
            Fax = Fax ?? string.Empty,
            Email = Email ?? string.Empty,
            CurrencyId = CurrencyId.GetValueOrDefault(),
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
            InvoiceCountryId = InvoiceCountryId.GetValueOrDefault(),
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
