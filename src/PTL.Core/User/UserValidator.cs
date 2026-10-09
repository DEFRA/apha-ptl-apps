namespace PTL.Core.User;

// Matches legacy Button_CreateUser_Click: only an email-address check (an AD account with no
// mail attribute cannot be onboarded) plus, newly, a server-side Department length check -
// legacy's TxtDept only had a client-side MaxLength=50 with no server-side re-validation
// (a flagged pre-existing gap, not carried forward here).
public static class UserValidator
{
    public const string NoEmailMessage = "The selected User has no Email Address stored in Active Directory and cannot be added.";
    public const string DuplicateMessage = "An account for this user already exists.";

    public static UserValidationResult Validate(string email, string department)
    {
        var errors = new List<UserValidationError>();

        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add(new UserValidationError(nameof(User.Email), NoEmailMessage));
        }

        if (department.Length > 50)
        {
            errors.Add(new UserValidationError(nameof(User.Department), "Department must be 50 characters or fewer"));
        }

        return new UserValidationResult(errors.Count == 0, errors);
    }
}

public sealed record UserValidationError(string Field, string Message);
public sealed record UserValidationResult(bool IsValid, IReadOnlyList<UserValidationError> Errors);
