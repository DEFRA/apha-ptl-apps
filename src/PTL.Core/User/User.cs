namespace PTL.Core.User;

// Keyless domain projection for the rows returned by spgaUser (tblUsers) - the internal PT-LIMS
// account record linked to a person's SSO/AD login. IsInactive/InactiveDate/AllocationCount back the Internal
// Test Consultant Department screen; they are only filled by the procedures that select them.
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

    // Only populated by spgaUserTestConsultant (the Internal Test Consultant Department grid): how many schemes
    // in the current and next year the consultant is allocated to, and which ones.
    public int AllocationCount { get; set; }
    public string? AllocationString { get; set; }
}
