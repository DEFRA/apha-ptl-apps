namespace PTL.Core.Customer;

// Participant-facing validation for EditCustomerDetails.aspx's submission (not the admin
// CustomerValidator - PendingCustomerUpdate is a distinct entity with slightly different required
// fields, e.g. InvoiceEmail is optional here, matching the legacy VB AddBusinessRules()/ASPX
// IsRequired flags exactly). The contact/address rules shared with the participant screen live
// in PendingUpdateFieldChecks.
public static class PendingCustomerUpdateValidator
{
    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        [nameof(PendingCustomerUpdate.ContactName)] = "Contact Name",
        [nameof(PendingCustomerUpdate.Organisation)] = "Organisation Name",
        [nameof(PendingCustomerUpdate.Address1)] = "Address 1",
        [nameof(PendingCustomerUpdate.Address2)] = "Address 2",
        [nameof(PendingCustomerUpdate.Address3)] = "Address 3",
        [nameof(PendingCustomerUpdate.Address4)] = "Address 4",
        [nameof(PendingCustomerUpdate.Address5)] = "Address 5",
        [nameof(PendingCustomerUpdate.CountryId)] = "Country",
        [nameof(PendingCustomerUpdate.Telephone)] = "Telephone",
        [nameof(PendingCustomerUpdate.Telephone2)] = "Telephone 2",
        [nameof(PendingCustomerUpdate.Email)] = "Email",
        [nameof(PendingCustomerUpdate.InvoiceName)] = "Invoice Contact Name",
        [nameof(PendingCustomerUpdate.InvoiceOrganisation)] = "Invoice Organisation Name",
        [nameof(PendingCustomerUpdate.InvoiceAddress1)] = "Invoice Address 1",
        [nameof(PendingCustomerUpdate.InvoiceAddress2)] = "Invoice Address 2",
        [nameof(PendingCustomerUpdate.InvoiceAddress3)] = "Invoice Address 3",
        [nameof(PendingCustomerUpdate.InvoiceAddress4)] = "Invoice Address 4",
        [nameof(PendingCustomerUpdate.InvoiceAddress5)] = "Invoice Address 5",
        [nameof(PendingCustomerUpdate.InvoiceCountryId)] = "Invoice Country",
        [nameof(PendingCustomerUpdate.InvoiceTelephone)] = "Invoice Telephone",
        [nameof(PendingCustomerUpdate.InvoiceTelephone2)] = "Invoice Telephone 2",
        [nameof(PendingCustomerUpdate.InvoiceEmail)] = "Invoice Email"
    };

    public static CustomerValidationResult Validate(PendingCustomerUpdate update)
    {
        var checks = new PendingUpdateFieldChecks(FieldLabels);

        checks.ValidateContactDetails(update);
        checks.MaxLength(update.Telephone2, 20, nameof(update.Telephone2));
        checks.MatchPhonePattern(update.Telephone2, nameof(update.Telephone2));

        checks.MaxLength(update.InvoiceName, 50, nameof(update.InvoiceName));
        checks.RequireNotEmpty(update.InvoiceOrganisation, nameof(update.InvoiceOrganisation));
        checks.MaxLength(update.InvoiceOrganisation, 50, nameof(update.InvoiceOrganisation));
        checks.RequireNotEmpty(update.InvoiceAddress1, nameof(update.InvoiceAddress1));
        checks.MaxLength(update.InvoiceAddress1, 100, nameof(update.InvoiceAddress1));
        checks.RequireNotEmpty(update.InvoiceAddress2, nameof(update.InvoiceAddress2));
        checks.MaxLength(update.InvoiceAddress2, 100, nameof(update.InvoiceAddress2));
        checks.MaxLength(update.InvoiceAddress3, 100, nameof(update.InvoiceAddress3));
        checks.MaxLength(update.InvoiceAddress4, 100, nameof(update.InvoiceAddress4));
        checks.MaxLength(update.InvoiceAddress5, 100, nameof(update.InvoiceAddress5));
        checks.RequireSelected(update.InvoiceCountryId, nameof(update.InvoiceCountryId));

        checks.MaxLength(update.InvoiceTelephone, 20, nameof(update.InvoiceTelephone));
        checks.MatchPhonePattern(update.InvoiceTelephone, nameof(update.InvoiceTelephone));
        checks.MaxLength(update.InvoiceTelephone2, 20, nameof(update.InvoiceTelephone2));
        checks.MatchPhonePattern(update.InvoiceTelephone2, nameof(update.InvoiceTelephone2));

        checks.MaxLength(update.InvoiceEmail, 150, nameof(update.InvoiceEmail));
        checks.RequireValidEmail(update.InvoiceEmail, nameof(update.InvoiceEmail));

        var errors = checks.Errors.Select(e => new CustomerValidationError(e.Field, e.Message)).ToList();
        return new CustomerValidationResult(errors.Count == 0, errors);
    }
}
