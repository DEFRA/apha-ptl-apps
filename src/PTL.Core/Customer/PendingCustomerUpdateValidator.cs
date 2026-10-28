using System.Text.RegularExpressions;

namespace PTL.Core.Customer;

// Participant-facing validation for EditCustomerDetails.aspx's submission (not the admin
// CustomerValidator - PendingCustomerUpdate is a distinct entity with slightly different required
// fields, e.g. InvoiceEmail is optional here, matching the legacy VB AddBusinessRules()/ASPX
// IsRequired flags exactly).
public static partial class PendingCustomerUpdateValidator
{
    private const string ContactNameField = "ContactName";
    private const string OrganisationField = "Organisation";
    private const string Address1Field = "Address1";
    private const string Address2Field = "Address2";
    private const string TelephoneField = "Telephone";
    private const string EmailField = "Email";
    private const string CountryIdField = "CountryId";
    private const string InvoiceOrganisationField = "InvoiceOrganisation";
    private const string InvoiceAddress1Field = "InvoiceAddress1";
    private const string InvoiceAddress2Field = "InvoiceAddress2";
    private const string InvoiceCountryIdField = "InvoiceCountryId";

    [GeneratedRegex(@"^[ 0-9\+\-\(\)\*\#]*$", RegexOptions.None, 1000)]
    private static partial Regex PhonePattern();

    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        [ContactNameField] = "Contact Name",
        [OrganisationField] = "Organisation Name",
        [Address1Field] = "Address 1",
        [Address2Field] = "Address 2",
        ["Address3"] = "Address 3",
        ["Address4"] = "Address 4",
        ["Address5"] = "Address 5",
        [CountryIdField] = "Country",
        [TelephoneField] = "Telephone",
        ["Telephone2"] = "Telephone 2",
        [EmailField] = "Email",
        ["InvoiceName"] = "Invoice Contact Name",
        [InvoiceOrganisationField] = "Invoice Organisation Name",
        [InvoiceAddress1Field] = "Invoice Address 1",
        [InvoiceAddress2Field] = "Invoice Address 2",
        ["InvoiceAddress3"] = "Invoice Address 3",
        ["InvoiceAddress4"] = "Invoice Address 4",
        ["InvoiceAddress5"] = "Invoice Address 5",
        [InvoiceCountryIdField] = "Invoice Country",
        ["InvoiceTelephone"] = "Invoice Telephone",
        ["InvoiceTelephone2"] = "Invoice Telephone 2",
        ["InvoiceEmail"] = "Invoice Email"
    };

    private static string Label(string field) => FieldLabels.GetValueOrDefault(field, field);

    public static CustomerValidationResult Validate(PendingCustomerUpdate update)
    {
        var errors = new List<CustomerValidationError>();

        RequireNotEmpty(update.ContactName, ContactNameField, errors);
        MaxLength(update.ContactName, 50, ContactNameField, errors);
        RequireNotEmpty(update.Organisation, OrganisationField, errors);
        MaxLength(update.Organisation, 50, OrganisationField, errors);
        RequireNotEmpty(update.Address1, Address1Field, errors);
        MaxLength(update.Address1, 100, Address1Field, errors);
        RequireNotEmpty(update.Address2, Address2Field, errors);
        MaxLength(update.Address2, 100, Address2Field, errors);
        MaxLength(update.Address3, 100, "Address3", errors);
        MaxLength(update.Address4, 100, "Address4", errors);
        MaxLength(update.Address5, 100, "Address5", errors);
        RequireSelected(update.CountryId, CountryIdField, errors);

        RequireNotEmpty(update.Telephone, TelephoneField, errors);
        MaxLength(update.Telephone, 20, TelephoneField, errors);
        RegexMatch(update.Telephone, TelephoneField, errors);
        MaxLength(update.Telephone2, 20, "Telephone2", errors);
        RegexMatch(update.Telephone2, "Telephone2", errors);

        RequireNotEmpty(update.Email, EmailField, errors);
        MaxLength(update.Email, 150, EmailField, errors);
        RequireValidEmail(update.Email, EmailField, errors);

        MaxLength(update.InvoiceName, 50, "InvoiceName", errors);
        RequireNotEmpty(update.InvoiceOrganisation, InvoiceOrganisationField, errors);
        MaxLength(update.InvoiceOrganisation, 50, InvoiceOrganisationField, errors);
        RequireNotEmpty(update.InvoiceAddress1, InvoiceAddress1Field, errors);
        MaxLength(update.InvoiceAddress1, 100, InvoiceAddress1Field, errors);
        RequireNotEmpty(update.InvoiceAddress2, InvoiceAddress2Field, errors);
        MaxLength(update.InvoiceAddress2, 100, InvoiceAddress2Field, errors);
        MaxLength(update.InvoiceAddress3, 100, "InvoiceAddress3", errors);
        MaxLength(update.InvoiceAddress4, 100, "InvoiceAddress4", errors);
        MaxLength(update.InvoiceAddress5, 100, "InvoiceAddress5", errors);
        RequireSelected(update.InvoiceCountryId, InvoiceCountryIdField, errors);

        MaxLength(update.InvoiceTelephone, 20, "InvoiceTelephone", errors);
        RegexMatch(update.InvoiceTelephone, "InvoiceTelephone", errors);
        MaxLength(update.InvoiceTelephone2, 20, "InvoiceTelephone2", errors);
        RegexMatch(update.InvoiceTelephone2, "InvoiceTelephone2", errors);

        MaxLength(update.InvoiceEmail, 150, "InvoiceEmail", errors);
        RequireValidEmail(update.InvoiceEmail, "InvoiceEmail", errors);

        return new CustomerValidationResult(errors.Count == 0, errors);
    }

    private static void RequireNotEmpty(string value, string field, List<CustomerValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new CustomerValidationError(field, $"Enter {Label(field)}"));
        }
    }

    private static void RequireSelected(Guid value, string field, List<CustomerValidationError> errors)
    {
        if (value == Guid.Empty)
        {
            errors.Add(new CustomerValidationError(field, $"Select {Label(field)}"));
        }
    }

    private static void MaxLength(string value, int max, string field, List<CustomerValidationError> errors)
    {
        if (value is not null && value.Length > max)
        {
            errors.Add(new CustomerValidationError(field, $"{Label(field)} must not exceed {max} characters"));
        }
    }

    private static void RegexMatch(string value, string field, List<CustomerValidationError> errors)
    {
        if (!string.IsNullOrEmpty(value) && !PhonePattern().IsMatch(value))
        {
            errors.Add(new CustomerValidationError(field, $"{Label(field)} contains characters that are not allowed"));
        }
    }

    // Optional field - only validated when a value is actually supplied (matches legacy, which
    // never marks InvoiceEmail/Email format rules as required-to-be-valid beyond "not blank").
    private static void RequireValidEmail(string value, string field, List<CustomerValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            _ = new System.Net.Mail.MailAddress(value);
        }
        catch (FormatException)
        {
            errors.Add(new CustomerValidationError(field, $"Enter a valid {Label(field)}"));
        }
    }
}
