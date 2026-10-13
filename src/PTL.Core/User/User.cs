namespace PTL.Core.User;

// Keyless domain projection for the rows returned by spgaUser (tblUsers) - the internal PT-LIMS
// account record linked to a person's SSO/AD login. IsInactive/InactiveDate/AllocationCount exist
// on the legacy UserDetails object too but are out of scope here - they belong to the separate
// "Remove User" story.
public sealed class User
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public bool IsInactive { get; set; }
    public DateTime? InactiveDate { get; set; }
}
