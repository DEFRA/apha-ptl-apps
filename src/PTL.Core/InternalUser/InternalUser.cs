namespace PTL.Core.InternalUser;

// tblUsers - legacy Windows/internal-staff authentication table. Identity fields are set-once at
// creation in the legacy app (only Department/IsInactive are editable post-creation via
// spuUserDept) - mirrored here as plain settable properties since this app only ever reads them.
public class InternalUser
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
    public Guid? SsoIdInt { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
}
