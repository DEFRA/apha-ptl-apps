using System.Text.RegularExpressions;

namespace PTL.Core.TestConsultant;

// Matches legacy ManageExternalTCs.aspx's own ValidatorNewNameRequired/ValidatorNewDepartmentRequired
// (shown but not enforced server-side) / ValidatorNewEmailRequired/ValidatorNewEmailRegEx - Name and
// Email are required per the story's Business Rule ("Each external Test Consultant requires Name
// and Email (Department is optional)"), Department stays optional.
public static partial class ExternalTestConsultantValidator
{
    public const string NameRequiredMessage = "Enter a name";
    public const string EmailRequiredMessage = "Enter an email address";
    public const string EmailInvalidMessage = "Enter an email address in the correct format";

    [GeneratedRegex(@"^[a-zA-Z0-9._%-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,4}$")]
    private static partial Regex EmailPattern();

    public static ExternalTestConsultantValidationResult Validate(string name, string email)
    {
        var errors = new List<ExternalTestConsultantValidationError>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new ExternalTestConsultantValidationError(nameof(TestConsultant.Name), NameRequiredMessage));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add(new ExternalTestConsultantValidationError(nameof(TestConsultant.Email), EmailRequiredMessage));
        }
        else if (!EmailPattern().IsMatch(email))
        {
            errors.Add(new ExternalTestConsultantValidationError(nameof(TestConsultant.Email), EmailInvalidMessage));
        }

        return new ExternalTestConsultantValidationResult(errors.Count == 0, errors);
    }
}

public sealed record ExternalTestConsultantValidationError(string Field, string Message);

public sealed record ExternalTestConsultantValidationResult(bool IsValid, IReadOnlyList<ExternalTestConsultantValidationError> Errors);
