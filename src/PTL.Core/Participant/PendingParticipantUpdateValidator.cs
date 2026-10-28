using System.Text.RegularExpressions;

namespace PTL.Core.Participant;

// Participant-facing validation for EditParticipantDetails.aspx's submission - mirrors legacy's
// AddBusinessRules()/ASPX RequiredFieldValidator flags exactly (ContactName, Organisation,
// Address1, Address2, Country, Telephone, Email are mandatory; Address3-5 and Email2 are not).
public static partial class PendingParticipantUpdateValidator
{
    private const string ContactNameField = "ContactName";
    private const string OrganisationField = "Organisation";
    private const string Address1Field = "Address1";
    private const string Address2Field = "Address2";
    private const string CountryIdField = "CountryId";
    private const string TelephoneField = "Telephone";
    private const string EmailField = "Email";

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
        [EmailField] = "Email (Primary)",
        ["Email2"] = "Email (Secondary)"
    };

    private static string Label(string field) => FieldLabels.GetValueOrDefault(field, field);

    public static ParticipantValidationResult Validate(PendingParticipantUpdate update)
    {
        var errors = new List<ParticipantValidationError>();

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

        RequireNotEmpty(update.Email, EmailField, errors);
        MaxLength(update.Email, 150, EmailField, errors);
        RequireValidEmail(update.Email, EmailField, errors);

        MaxLength(update.Email2, 150, "Email2", errors);
        RequireValidEmail(update.Email2, "Email2", errors);

        return new ParticipantValidationResult(errors.Count == 0, errors);
    }

    private static void RequireNotEmpty(string value, string field, List<ParticipantValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ParticipantValidationError(field, $"Enter {Label(field)}"));
        }
    }

    private static void RequireSelected(Guid value, string field, List<ParticipantValidationError> errors)
    {
        if (value == Guid.Empty)
        {
            errors.Add(new ParticipantValidationError(field, $"Select {Label(field)}"));
        }
    }

    private static void MaxLength(string value, int max, string field, List<ParticipantValidationError> errors)
    {
        if (value is not null && value.Length > max)
        {
            errors.Add(new ParticipantValidationError(field, $"{Label(field)} must not exceed {max} characters"));
        }
    }

    private static void RegexMatch(string value, string field, List<ParticipantValidationError> errors)
    {
        if (!string.IsNullOrEmpty(value) && !PhonePattern().IsMatch(value))
        {
            errors.Add(new ParticipantValidationError(field, $"{Label(field)} contains characters that are not allowed"));
        }
    }

    // Optional field - only validated when a value is actually supplied.
    private static void RequireValidEmail(string value, string field, List<ParticipantValidationError> errors)
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
            errors.Add(new ParticipantValidationError(field, $"Enter a valid {Label(field)}"));
        }
    }
}
