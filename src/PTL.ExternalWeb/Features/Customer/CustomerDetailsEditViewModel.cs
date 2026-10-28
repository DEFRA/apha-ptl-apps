using Microsoft.AspNetCore.Mvc.Rendering;

namespace PTL.ExternalWeb.Features.Customer;

/// <summary>View model for the participant-facing Edit Customer/Invoice Details screen (legacy
/// EditCustomerDetails.aspx).</summary>
public sealed class CustomerDetailsEditViewModel
{
    public string QalNumber { get; set; } = string.Empty;

    /// <summary>True when a submitted-but-undecided pending update already exists - the form is
    /// shown read-only with a notice (legacy DisableForm()/NotificationMessage).</summary>
    public bool HasOutstandingUpdate { get; set; }

    public string ContactName { get; set; } = string.Empty;
    public string Organisation { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string Address3 { get; set; } = string.Empty;
    public string Address4 { get; set; } = string.Empty;
    public string Address5 { get; set; } = string.Empty;
    public Guid? CountryId { get; set; }
    public string Telephone { get; set; } = string.Empty;
    public string Telephone2 { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string InvoiceName { get; set; } = string.Empty;
    public string InvoiceOrganisation { get; set; } = string.Empty;
    public string InvoiceAddress1 { get; set; } = string.Empty;
    public string InvoiceAddress2 { get; set; } = string.Empty;
    public string InvoiceAddress3 { get; set; } = string.Empty;
    public string InvoiceAddress4 { get; set; } = string.Empty;
    public string InvoiceAddress5 { get; set; } = string.Empty;
    public Guid? InvoiceCountryId { get; set; }
    public string InvoiceTelephone { get; set; } = string.Empty;
    public string InvoiceTelephone2 { get; set; } = string.Empty;
    public string InvoiceEmail { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> CountryOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> InvoiceCountryOptions { get; set; } = [];
}
