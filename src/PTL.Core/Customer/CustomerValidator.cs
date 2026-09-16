using System.Text.RegularExpressions;

namespace PTL.Core.Customer;

// Preserves the validation rules from PtaBusinessObjects.BusinessObjects.Contracts.Customer.AddBusinessRules()
// (see docs/analysis/customer-analysis.md, "Validation Rules"). Field lengths match the
// spiCustomer/spuCustomer stored procedure parameter sizes (and therefore the tblCustomer columns).
public static partial class CustomerValidator
{
    [GeneratedRegex(@"^(QAL/[0-9]*)?$")]
    private static partial Regex RegisteredFileNumberPattern();

    [GeneratedRegex(@"^[ 0-9\+\-\(\)\*\#]*$")]
    private static partial Regex PhonePattern();

    public static CustomerValidationResult Validate(Customer customer)
    {
        var errors = new List<CustomerValidationError>();

        if (customer.CustomerTypeId == Guid.Empty)
        {
            errors.Add(new CustomerValidationError("CustomerTypeId", "CustomerTypeId cannot be an empty GUID."));
        }

        RequireNotEmpty(customer.Name, "Name", errors);
        MaxLength(customer.Name, 50, "Name", errors);
        MaxLength(customer.ContactName, 50, "ContactName", errors);
        MaxLength(customer.Organisation, 50, "Organisation", errors);

        MaxLength(customer.RegisteredFileNumber, 10, "RegisteredFileNumber", errors);
        if (!RegisteredFileNumberPattern().IsMatch(customer.RegisteredFileNumber))
        {
            errors.Add(new CustomerValidationError("RegisteredFileNumber", "RegisteredFileNumber must match the format QAL/nnnnn."));
        }

        MaxLength(customer.Address1, 100, "Address1", errors);
        MaxLength(customer.Address2, 100, "Address2", errors);
        MaxLength(customer.Address3, 100, "Address3", errors);
        MaxLength(customer.Address4, 100, "Address4", errors);
        MaxLength(customer.Address5, 100, "Address5", errors);

        MaxLength(customer.Telephone, 20, "Telephone", errors);
        RegexMatch(customer.Telephone, PhonePattern(), "Telephone", errors);
        MaxLength(customer.Telephone2, 20, "Telephone2", errors);
        RegexMatch(customer.Telephone2, PhonePattern(), "Telephone2", errors);
        MaxLength(customer.Fax, 20, "Fax", errors);
        RegexMatch(customer.Fax, PhonePattern(), "Fax", errors);

        MaxLength(customer.Email, 150, "Email", errors);
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
            RequireNotEmpty(customer.Telephone, "Telephone", errors);
            RequireNotEmpty(customer.Email, "Email", errors);
            RequireNotEmpty(customer.InvoiceOrganisation, "InvoiceOrganisation", errors);
            RequireNotEmpty(customer.InvoiceAddress1, "InvoiceAddress1", errors);
            RequireNotEmpty(customer.InvoiceAddress2, "InvoiceAddress2", errors);

            RequireValidEmail(customer.Email, "Email", errors);
            RequireValidEmail(customer.InvoiceEmail, "InvoiceEmail", errors);
        }
        else if (customer.CustomerStatusId is null || customer.CustomerStatusId == Guid.Empty)
        {
            // Mirrors Customer.aspx.vb's LoadStatusValues/inactive-error flag: an inactive customer
            // must record which CustomerStatus (inactive reason) applies.
            errors.Add(new CustomerValidationError("CustomerStatusId", "Select a customer status when the customer is inactive."));
        }

        return new CustomerValidationResult(errors.Count == 0, errors);
    }

    private static void RequireNotEmpty(string value, string field, List<CustomerValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new CustomerValidationError(field, $"{field} is required."));
        }
    }

    private static void MaxLength(string value, int max, string field, List<CustomerValidationError> errors)
    {
        if (value.Length > max)
        {
            errors.Add(new CustomerValidationError(field, $"{field} must not exceed {max} characters."));
        }
    }

    private static void RegexMatch(string value, Regex pattern, string field, List<CustomerValidationError> errors)
    {
        if (!string.IsNullOrEmpty(value) && !pattern.IsMatch(value))
        {
            errors.Add(new CustomerValidationError(field, $"{field} contains characters that are not allowed."));
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
            errors.Add(new CustomerValidationError(field, $"{field} must be a valid email address."));
        }
    }
}
