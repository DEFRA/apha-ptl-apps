namespace PTL.Auth.Entra;

/// <summary>
/// Builds the legacy <c>DEMETER\employeeId</c> username an internal user is stored under when the Entra ID token carries
/// no on-premises account claims and the directory's employee id is used instead.
/// </summary>
public static class EmployeeUserName
{
    /// <summary>The legacy domain prefix of an employee-id based username.</summary>
    public const string Domain = "DEMETER";

    // The username column is 50 characters, so the id must leave room for the "DEMETER\" prefix.
    private const int MaxEmployeeIdLength = 40;

    /// <summary>Creates the username for a directory employee id.</summary>
    /// <param name="employeeId">The employee id read from the directory.</param>
    /// <returns>The <c>DEMETER\employeeId</c> username, or <see langword="null"/> when the id is missing or has unexpected characters.</returns>
    public static string? FromEmployeeId(string? employeeId)
    {
        var id = employeeId?.Trim();

        return id is { Length: > 0 and <= MaxEmployeeIdLength } && id.All(IsPermitted)
            ? $"{Domain}\\{id}"
            : null;
    }

    private static bool IsPermitted(char character) => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-';
}
