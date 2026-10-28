using System.Text.RegularExpressions;

namespace PTL.Core;

// The contact/address fields both participant-facing "propose a change" screens share
// (PendingCustomerUpdate, PendingParticipantUpdate).
public interface IPendingContactDetails
{
    string ContactName { get; }
    string Organisation { get; }
    string Address1 { get; }
    string Address2 { get; }
    string Address3 { get; }
    string Address4 { get; }
    string Address5 { get; }
    Guid CountryId { get; }
    string Telephone { get; }
    string Email { get; }
}

// Field rules shared by PendingCustomerUpdateValidator and PendingParticipantUpdateValidator, so the
// two stay in step; each validator maps Errors onto its own error type.
internal sealed partial class PendingUpdateFieldChecks(IReadOnlyDictionary<string, string> labels)
{
    [GeneratedRegex(@"^[ 0-9\+\-\(\)\*\#]*$", RegexOptions.None, 1000)]
    private static partial Regex PhonePattern();

    public List<(string Field, string Message)> Errors { get; } = [];

    private string Label(string field) => labels.GetValueOrDefault(field, field);

    public void ValidateContactDetails(IPendingContactDetails details)
    {
        RequireNotEmpty(details.ContactName, nameof(IPendingContactDetails.ContactName));
        MaxLength(details.ContactName, 50, nameof(IPendingContactDetails.ContactName));
        RequireNotEmpty(details.Organisation, nameof(IPendingContactDetails.Organisation));
        MaxLength(details.Organisation, 50, nameof(IPendingContactDetails.Organisation));
        RequireNotEmpty(details.Address1, nameof(IPendingContactDetails.Address1));
        MaxLength(details.Address1, 100, nameof(IPendingContactDetails.Address1));
        RequireNotEmpty(details.Address2, nameof(IPendingContactDetails.Address2));
        MaxLength(details.Address2, 100, nameof(IPendingContactDetails.Address2));
        MaxLength(details.Address3, 100, nameof(IPendingContactDetails.Address3));
        MaxLength(details.Address4, 100, nameof(IPendingContactDetails.Address4));
        MaxLength(details.Address5, 100, nameof(IPendingContactDetails.Address5));
        RequireSelected(details.CountryId, nameof(IPendingContactDetails.CountryId));

        RequireNotEmpty(details.Telephone, nameof(IPendingContactDetails.Telephone));
        MaxLength(details.Telephone, 20, nameof(IPendingContactDetails.Telephone));
        MatchPhonePattern(details.Telephone, nameof(IPendingContactDetails.Telephone));

        RequireNotEmpty(details.Email, nameof(IPendingContactDetails.Email));
        MaxLength(details.Email, 150, nameof(IPendingContactDetails.Email));
        RequireValidEmail(details.Email, nameof(IPendingContactDetails.Email));
    }

    public void RequireNotEmpty(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Errors.Add((field, $"Enter {Label(field)}"));
        }
    }

    public void RequireSelected(Guid value, string field)
    {
        if (value == Guid.Empty)
        {
            Errors.Add((field, $"Select {Label(field)}"));
        }
    }

    public void MaxLength(string value, int max, string field)
    {
        if (value is not null && value.Length > max)
        {
            Errors.Add((field, $"{Label(field)} must not exceed {max} characters"));
        }
    }

    public void MatchPhonePattern(string value, string field)
    {
        if (!string.IsNullOrEmpty(value) && !PhonePattern().IsMatch(value))
        {
            Errors.Add((field, $"{Label(field)} contains characters that are not allowed"));
        }
    }

    // Only validated when a value is actually supplied; pair with RequireNotEmpty for mandatory emails.
    public void RequireValidEmail(string value, string field)
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
            Errors.Add((field, $"Enter a valid {Label(field)}"));
        }
    }
}
