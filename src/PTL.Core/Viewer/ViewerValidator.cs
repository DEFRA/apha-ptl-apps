using System.Text.RegularExpressions;

namespace PTL.Core.Viewer;

// Matches legacy ManageViewers.aspx's ValidatorNewNameRequired/ValidatorNewEmailRequired/
// ValidatorNewEmailRegEx - Name and Email are both required per the story's Business Rule
// ("Each Viewer requires Name and Email").
public static partial class ViewerValidator
{
    public const string NameRequiredMessage = "Enter a name";
    public const string EmailRequiredMessage = "Enter an email address";
    public const string EmailInvalidMessage = "Enter an email address in the correct format";

    [GeneratedRegex(@"^[a-zA-Z0-9._%-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,4}$")]
    private static partial Regex EmailPattern();

    public static ViewerValidationResult Validate(string name, string email)
    {
        var errors = new List<ViewerValidationError>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new ViewerValidationError(nameof(ViewerEntity.Name), NameRequiredMessage));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add(new ViewerValidationError(nameof(ViewerEntity.Email), EmailRequiredMessage));
        }
        else if (!EmailPattern().IsMatch(email))
        {
            errors.Add(new ViewerValidationError(nameof(ViewerEntity.Email), EmailInvalidMessage));
        }

        return new ViewerValidationResult(errors.Count == 0, errors);
    }
}

public sealed record ViewerValidationError(string Field, string Message);

public sealed record ViewerValidationResult(bool IsValid, IReadOnlyList<ViewerValidationError> Errors);
