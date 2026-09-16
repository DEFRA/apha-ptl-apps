namespace PTL.Core.Customer;

// Domain entity mapped to tblCustomer by PTL.Data's PtlDbContext; shape matches the full row
// returned by the spgCustomerByCustomerId stored procedure.
public class Customer
{
    public Guid CustomerId { get; set; }
    public string QalNumber { get; set; } = string.Empty;
    public string RegisteredFileNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PreviousName { get; set; } = string.Empty;
    public Guid CustomerTypeId { get; set; }
    public string VatNumber { get; set; } = string.Empty;
    public Guid VatRatingId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string CustomerFinanceId { get; set; } = string.Empty;
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
    public Guid CurrencyId { get; set; }
    public string Comments { get; set; } = string.Empty;
    public DateTime InitialStartDate { get; set; }
    public string PostageArrangements { get; set; } = string.Empty;
    public bool PaymentNonUK { get; set; }
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
    public bool IsActive { get; set; }
    public bool CanOrderOnline { get; set; }
    public DateTime? InactiveDate { get; set; }
    public Guid? CustomerStatusId { get; set; }
}
