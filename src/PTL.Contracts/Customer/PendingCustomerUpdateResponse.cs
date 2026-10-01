namespace PTL.Contracts.Customer;

// One row on the "Review Pending Customer Updates" list (legacy ReviewPendingCustomerUpdates.aspx /
// spgaPendingCustomerDetailsEditInfo).
public sealed record PendingCustomerUpdateSummaryResponse(
    Guid CustomerId,
    string QalNumber,
    string Name);

// The proposed changes submitted by an external participant, awaiting admin approval/decline
// (legacy tblPendingCustomerDetailsEdit / spgPendingCustomerDetailsEditByCustomerID). Field set
// matches PendingCustomerUpdateDetails.aspx exactly - no Comments/financial/status fields, since
// legacy only lets participants propose contact and invoice-contact changes.
public sealed record PendingCustomerUpdateResponse(
    Guid CustomerId,
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
    string InvoiceEmail);

// Current customer record + the pending change proposed against it, for the
// PendingCustomerUpdateDetails comparison page.
public sealed record PendingCustomerUpdateComparisonResponse(
    CustomerResponse Current,
    PendingCustomerUpdateResponse Pending);

// Amended pending values submitted alongside an Approve, mirroring legacy
// PendingCustomerUpdateDetails.aspx's ButtonApprove_Click, which writes the on-screen values to
// both the pending record and the live customer rather than approving the stored values verbatim.
public sealed record PendingCustomerUpdateSaveRequest(
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
    string InvoiceEmail);

// Outcome of an Approve/Decline. NotFound distinguishes "no outstanding pending update" from a
// validation failure so the caller can return 404 rather than redisplaying the form.
public sealed record PendingCustomerUpdateDecisionResult(
    bool Success,
    bool NotFound,
    IReadOnlyDictionary<string, string[]> FieldErrors);
