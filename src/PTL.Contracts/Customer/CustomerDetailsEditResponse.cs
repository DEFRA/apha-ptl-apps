namespace PTL.Contracts.Customer;

// Participant-facing read model for GET /api/customer-details-edit/{participantId} - legacy
// EditCustomerDetails.aspx. Values come from the outstanding PendingCustomerUpdate when one exists
// (HasOutstandingUpdate = true), otherwise from the live Customer record.
public sealed record CustomerDetailsEditResponse(
    string QalNumber,
    bool HasOutstandingUpdate,
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
    string Email,
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
    string InvoiceEmail);

public sealed record CustomerDetailsEditSaveRequest(
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
    string Email,
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
    string InvoiceEmail);

public sealed record CustomerDetailsEditSaveResult(bool Success, IReadOnlyDictionary<string, string[]> Errors);
