namespace PTL.Core.Customer;

// Domain entity mapped to tblPendingCustomerDetailsEdit - a participant-proposed change to a
// Customer record awaiting admin approval/decline (legacy PendingCustomerUpdate.vb /
// spgPendingCustomerDetailsEditByCustomerID). Only IsSubmitted=1, IsDeleted=0 rows are "live"
// pending updates - see IPendingCustomerUpdateRepository.
public class PendingCustomerUpdate
{
    public Guid PendingCustomerUpdateId { get; set; }
    public Guid CustomerId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Organisation { get; set; } = string.Empty;
    public string Address1 { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string Address3 { get; set; } = string.Empty;
    public string Address4 { get; set; } = string.Empty;
    public string Address5 { get; set; } = string.Empty;
    public Guid CountryId { get; set; }
    public string Telephone { get; set; } = string.Empty;
    public string Telephone2 { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string InvoiceName { get; set; } = string.Empty;
    public string InvoiceOrganisation { get; set; } = string.Empty;
    public string InvoiceAddress1 { get; set; } = string.Empty;
    public string InvoiceAddress2 { get; set; } = string.Empty;
    public string InvoiceAddress3 { get; set; } = string.Empty;
    public string InvoiceAddress4 { get; set; } = string.Empty;
    public string InvoiceAddress5 { get; set; } = string.Empty;
    public Guid InvoiceCountryId { get; set; }
    public string InvoiceTelephone { get; set; } = string.Empty;
    public string InvoiceTelephone2 { get; set; } = string.Empty;
    public string InvoiceFax { get; set; } = string.Empty;
    public string InvoiceEmail { get; set; } = string.Empty;
    public bool IsSubmitted { get; set; }
    public bool IsDeleted { get; set; }
}
