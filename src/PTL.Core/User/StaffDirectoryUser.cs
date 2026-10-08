namespace PTL.Core.User;

// A candidate person found by searching the staff directory, not yet linked to a PT-LIMS account.
public sealed class StaffDirectoryUser
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
