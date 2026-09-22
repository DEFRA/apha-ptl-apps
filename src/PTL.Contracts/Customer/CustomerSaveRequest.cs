namespace PTL.Contracts.Customer;

// Field set mirrors CustomerResponse minus server-generated values (CustomerId, QalNumber,
// InitialStartDate). Shared by both create and update actions since the editable field set is
// identical for both operations. IsActive/CanOrderOnline/CustomerStatusId default values match
// Customer.DataPortal_Create() in the legacy app, which always creates a new customer as active
// unless the form explicitly unchecks it; update callers always pass every field explicitly so
// the defaults have no effect there. Named CustomerSaveRequest (not CustomerRequest) because
// CustomerRequest is already the GET /api/customers query-filter contract.
public sealed record CustomerSaveRequest(
    string RegisteredFileNumber,
    string Name,
    string PreviousName,
    Guid CustomerTypeId,
    string VatNumber,
    Guid VatRatingId,
    string AccountNumber,
    string CustomerFinanceId,
    string ContactName,
    string Organisation,
    string Address1,
    string Address2,
    string Address3,
    string Address4,
    string Address5,
    Guid CountryId,
    string Telephone,
    string Telephone2,
    string Fax,
    string Email,
    Guid CurrencyId,
    string Comments,
    string PostageArrangements,
    bool PaymentNonUK,
    string InvoiceName,
    string InvoiceOrganisation,
    string InvoiceAddress1,
    string InvoiceAddress2,
    string InvoiceAddress3,
    string InvoiceAddress4,
    string InvoiceAddress5,
    Guid InvoiceCountryId,
    string InvoiceTelephone,
    string InvoiceTelephone2,
    string InvoiceFax,
    string InvoiceEmail,
    bool IsActive = true,
    bool CanOrderOnline = false,
    Guid? CustomerStatusId = null);
