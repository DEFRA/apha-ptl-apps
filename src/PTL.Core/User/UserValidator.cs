namespace PTL.Core.User;

// Matches legacy Button_CreateUser_Click: an email-address check (an AD account with no mail
// attribute cannot be onboarded). Legacy had no server-side length checks - TxtDept only had a
// client-side MaxLength=50, and the other values went straight to spiUser's varchar(50)
// parameters, so an over-long value failed in the database. Those gaps are not carried forward:
// every value is checked against the spiUser column length here.
public static class UserValidator
{
    public const string NoEmailMessage = "The selected User has no Email Address stored in Active Directory and cannot be added.";
    public const string DuplicateMessage = "An account for this user already exists.";
    public const int MaxLength = 50;

    public static UserValidationResult Validate(NewUserDetails details)
    {
        var errors = new List<UserValidationError>();

        if (string.IsNullOrWhiteSpace(details.Email))
        {
            errors.Add(new UserValidationError(nameof(User.Email), NoEmailMessage));
        }

        CheckLength(errors, nameof(User.Username), "Username", details.Username);
        CheckLength(errors, nameof(User.Email), "Email address", details.Email);
        CheckLength(errors, nameof(User.FriendlyName), "Display name", details.FriendlyName);
        CheckLength(errors, nameof(User.FirstName), "First name", details.FirstName);
        CheckLength(errors, nameof(User.LastName), "Last name", details.LastName);
        CheckLength(errors, nameof(User.Department), "Department", details.Department.Trim());

        return new UserValidationResult(errors.Count == 0, errors);
    }

    private static void CheckLength(List<UserValidationError> errors, string field, string label, string value)
    {
        if (value.Length > MaxLength)
        {
            errors.Add(new UserValidationError(field, $"{label} must be {MaxLength} characters or fewer"));
        }
    }
}

public sealed record UserValidationError(string Field, string Message);
public sealed record UserValidationResult(bool IsValid, IReadOnlyList<UserValidationError> Errors);
