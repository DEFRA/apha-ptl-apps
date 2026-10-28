using Microsoft.AspNetCore.Mvc.Rendering;
using PTL.ExternalWeb.Features.MyAccount;

namespace PTL.ExternalWeb.Features.Customer;

/// <summary>View model for the participant-facing Edit Customer/Invoice Details screen (legacy
/// EditCustomerDetails.aspx).</summary>
public sealed class CustomerDetailsEditViewModel : AccountDetailsEditViewModelBase
{
    public string QalNumber { get; set; } = string.Empty;

    public string? Telephone2 { get; set; } = string.Empty;

    public string? InvoiceName { get; set; } = string.Empty;
    public string? InvoiceOrganisation { get; set; } = string.Empty;
    public string? InvoiceAddress1 { get; set; } = string.Empty;
    public string? InvoiceAddress2 { get; set; } = string.Empty;
    public string? InvoiceAddress3 { get; set; } = string.Empty;
    public string? InvoiceAddress4 { get; set; } = string.Empty;
    public string? InvoiceAddress5 { get; set; } = string.Empty;
    public Guid? InvoiceCountryId { get; set; }
    public string? InvoiceTelephone { get; set; } = string.Empty;
    public string? InvoiceTelephone2 { get; set; } = string.Empty;
    public string? InvoiceEmail { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> InvoiceCountryOptions { get; set; } = [];
}
