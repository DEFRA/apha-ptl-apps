using System.Text.RegularExpressions;
using PTL.Contracts.LabUsers;

namespace PTL.Core.LabUsers;

/// <summary>The rules for a lab user's name and email, with the messages shown to the participant.</summary>
public static partial class LabUserValidator
{
    public const string NameField = "Name";
    public const string EmailField = "Email";

    public const string NameRequiredMessage = "Enter a name";
    public const string NameTooLongMessage = "Name must be 255 characters or fewer";
    public const string NameInvalidMessage = "Enter a name without line breaks or special characters";
    public const string EmailRequiredMessage = "Enter an email address";
    public const string EmailInvalidMessage = "Enter an email address in the correct format, like name@example.com";
    public const string EmailTooLongMessage = "Email address must be 254 characters or fewer";

    // One address, no spaces or control characters, a dot in the domain: just enough to stop typing mistakes and
    // anything that is not a single address. CIDM and the email itself prove the address is real.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s.]+$", RegexOptions.None, matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailPattern();

    /// <summary>The errors keyed by field name; empty when valid.</summary>
    public static Dictionary<string, string[]> Validate(string name, string email)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors[NameField] = [NameRequiredMessage];
        }
        else if (name.Any(char.IsControl))
        {
            errors[NameField] = [NameInvalidMessage];
        }
        else if (name.Trim().Length > LabUserSaveRequest.MaxNameLength)
        {
            errors[NameField] = [NameTooLongMessage];
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            errors[EmailField] = [EmailRequiredMessage];
        }
        else if (email.Trim().Length > LabUserSaveRequest.MaxEmailLength)
        {
            errors[EmailField] = [EmailTooLongMessage];
        }
        else if (!EmailPattern().IsMatch(email.Trim()))
        {
            errors[EmailField] = [EmailInvalidMessage];
        }

        return errors;
    }
}
