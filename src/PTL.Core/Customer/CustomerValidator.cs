using System.Text.RegularExpressions;

namespace PTL.Core.Customer;

// Preserves the validation rules from PtaBusinessObjects.BusinessObjects.Contracts.Customer.AddBusinessRules()
// (see docs/analysis/customer-analysis.md, "Validation Rules"). Field lengths match the
// spiCustomer/spuCustomer stored procedure parameter sizes (and therefore the tblCustomer columns).
public static partial class CustomerValidator
{
    private const string OrganisationField = "Organisation";
    private const string RegisteredFileNumberField = "RegisteredFileNumber";
    private const string TelephoneField = "Telephone";
    private const string EmailField = "Email";

    [GeneratedRegex(@"^(QAL/[0-9]*)?$", RegexOptions.None, 1000)]
    private static partial Regex RegisteredFileNumberPattern();

    [GeneratedRegex(@"^[ 0-9\+\-\(\)\*\#]*$", RegexOptions.None, 1000)]
    private static partial Regex PhonePattern();

    // Human-readable labels matching each field's <label> text in _CustomerForm.cshtml, so
    // validation messages read naturally (e.g. "VAT number must not exceed...") instead of
    // exposing the raw PascalCase property name to the user.
    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        ["Name"] = "Name",
        ["ContactName"] = "Contact name",
        [OrganisationField] = "Organisation",
        [RegisteredFileNumberField] = "Registered file number",
        ["Address1"] = "Address line 1",
        ["Address2"] = "Address line 2",
        ["Address3"] = "Address line 3",
        ["Address4"] = "Address line 4",
        ["Address5"] = "Address line 5",
        [TelephoneField] = "Telephone",
        ["Telephone2"] = "Telephone (alternative)",
        ["Fax"] = "Fax",
        [EmailField] = "Email",
        ["Comments"] = "Comments",
        ["PostageArrangements"] = "Postage arrangements",
        ["InvoiceName"] = "Invoice name",
        ["InvoiceOrganisation"] = "Invoice organisation",
        ["InvoiceAddress1"] = "Invoice address line 1",
        ["InvoiceAddress2"] = "Invoice address line 2",
        ["InvoiceAddress3"] = "Invoice address line 3",
        ["InvoiceAddress4"] = "Invoice address line 4",
        ["InvoiceAddress5"] = "Invoice address line 5",
        ["InvoiceTelephone"] = "Invoice telephone",
        ["InvoiceTelephone2"] = "Invoice telephone (alternative)",
        ["InvoiceFax"] = "Invoice fax",
        ["InvoiceEmail"] = "Invoice email",
        ["CountryId"] = "Country",
        ["InvoiceCountryId"] = "Invoice country",
        ["VatNumber"] = "VAT number",
        ["AccountNumber"] = "Account number",
        ["CustomerFinanceId"] = "Customer finance ID",
        ["CustomerTypeId"] = "Customer type"
    };

    private static string Label(string field) => FieldLabels.GetValueOrDefault(field, field);

    public static CustomerValidationResult Validate(Customer customer)
    {
        var errors = new List<CustomerValidationError>();

        if (customer.CustomerTypeId == Guid.Empty)
        {
            errors.Add(new CustomerValidationError("CustomerTypeId", $"{Label("CustomerTypeId")} must be selected"));
        }

        RequireNotEmpty(customer.Name, "Name", errors);
        MaxLength(customer.Name, 50, "Name", errors);
        MaxLength(customer.ContactName, 50, "ContactName", errors);
        MaxLength(customer.Organisation, 50, OrganisationField, errors);

        MaxLength(customer.RegisteredFileNumber, 10, RegisteredFileNumberField, errors);
        if (!RegisteredFileNumberPattern().IsMatch(customer.RegisteredFileNumber ?? string.Empty))
        {
            errors.Add(new CustomerValidationError(RegisteredFileNumberField, $"{Label(RegisteredFileNumberField)} must match the format QAL/nnnnn"));
        }

        MaxLength(customer.Address1, 100, "Address1", errors);
        MaxLength(customer.Address2, 100, "Address2", errors);
        MaxLength(customer.Address3, 100, "Address3", errors);
        MaxLength(customer.Address4, 100, "Address4", errors);
        MaxLength(customer.Address5, 100, "Address5", errors);

        MaxLength(customer.Telephone, 20, TelephoneField, errors);
        RegexMatch(customer.Telephone, PhonePattern(), TelephoneField, errors);
        MaxLength(customer.Telephone2, 20, "Telephone2", errors);
        RegexMatch(customer.Telephone2, PhonePattern(), "Telephone2", errors);
        MaxLength(customer.Fax, 20, "Fax", errors);
        RegexMatch(customer.Fax, PhonePattern(), "Fax", errors);

        MaxLength(customer.Email, 150, EmailField, errors);
        MaxLength(customer.Comments, 2000, "Comments", errors);
        MaxLength(customer.PostageArrangements, 500, "PostageArrangements", errors);

        MaxLength(customer.InvoiceName, 50, "InvoiceName", errors);
        MaxLength(customer.InvoiceOrganisation, 50, "InvoiceOrganisation", errors);
        MaxLength(customer.InvoiceAddress1, 100, "InvoiceAddress1", errors);
        MaxLength(customer.InvoiceAddress2, 100, "InvoiceAddress2", errors);
        MaxLength(customer.InvoiceAddress3, 100, "InvoiceAddress3", errors);
        MaxLength(customer.InvoiceAddress4, 100, "InvoiceAddress4", errors);
        MaxLength(customer.InvoiceAddress5, 100, "InvoiceAddress5", errors);
        MaxLength(customer.InvoiceTelephone, 20, "InvoiceTelephone", errors);
        RegexMatch(customer.InvoiceTelephone, PhonePattern(), "InvoiceTelephone", errors);
        MaxLength(customer.InvoiceTelephone2, 20, "InvoiceTelephone2", errors);
        RegexMatch(customer.InvoiceTelephone2, PhonePattern(), "InvoiceTelephone2", errors);
        MaxLength(customer.InvoiceFax, 20, "InvoiceFax", errors);
        RegexMatch(customer.InvoiceFax, PhonePattern(), "InvoiceFax", errors);
        MaxLength(customer.InvoiceEmail, 150, "InvoiceEmail", errors);

        MaxLength(customer.VatNumber, 20, "VatNumber", errors);
        MaxLength(customer.AccountNumber, 20, "AccountNumber", errors);
        MaxLength(customer.CustomerFinanceId, 30, "CustomerFinanceId", errors);

        // Required only while the customer is active (matches the legacy "If IsActive Then ..." block).
        if (customer.IsActive)
        {
            RequireNotEmpty(customer.ContactName, "ContactName", errors);
            RequireNotEmpty(customer.Organisation, "Organisation", errors);
            RequireNotEmpty(customer.Address1, "Address1", errors);
            RequireNotEmpty(customer.Address2, "Address2", errors);
            RequireSelected(customer.CountryId, "CountryId", errors);
            RequireNotEmpty(customer.Telephone, TelephoneField, errors);
            RequireNotEmpty(customer.Email, EmailField, errors);
            RequireNotEmpty(customer.InvoiceOrganisation, "InvoiceOrganisation", errors);
            RequireNotEmpty(customer.InvoiceAddress1, "InvoiceAddress1", errors);
            RequireNotEmpty(customer.InvoiceAddress2, "InvoiceAddress2", errors);
            RequireNotEmpty(customer.InvoiceEmail, "InvoiceEmail", errors);
            RequireSelected(customer.InvoiceCountryId, "InvoiceCountryId", errors);

            RequireValidEmail(customer.Email, EmailField, errors);
            RequireValidEmail(customer.InvoiceEmail, "InvoiceEmail", errors);
        }

        return new CustomerValidationResult(errors.Count == 0, errors);
    }

    // `value` is declared non-nullable, but callers ultimately originate from JSON request bodies -
    // a property omitted from the payload deserializes to null despite the C# annotation, so every
    // helper below must tolerate that at runtime rather than relying on the compile-time type alone.
    private static void RequireNotEmpty(string value, string field, List<CustomerValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new CustomerValidationError(field, $"{Label(field)} is required"));
        }
    }

    private static void RequireSelected(Guid value, string field, List<CustomerValidationError> errors)
    {
        if (value == Guid.Empty)
        {
            errors.Add(new CustomerValidationError(field, $"{Label(field)} must be selected"));
        }
    }

    private static void MaxLength(string value, int max, string field, List<CustomerValidationError> errors)
    {
        if (value is not null && value.Length > max)
        {
            errors.Add(new CustomerValidationError(field, $"{Label(field)} must not exceed {max} characters"));
        }
    }

    private static void RegexMatch(string value, Regex pattern, string field, List<CustomerValidationError> errors)
    {
        if (!string.IsNullOrEmpty(value) && !pattern.IsMatch(value))
        {
            errors.Add(new CustomerValidationError(field, $"{Label(field)} contains characters that are not allowed"));
        }
    }

    // The exact legacy EmailRegEx resource value was not available for extraction; this uses
    // System.Net.Mail's parser as a functionally-equivalent format check.
    // [NEEDS INVESTIGATION]: confirm against ProficiencyTestingResources.GlobalResources.EmailRegEx.
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
            errors.Add(new CustomerValidationError(field, $"{Label(field)} must be a valid email address"));
        }
    }
}
