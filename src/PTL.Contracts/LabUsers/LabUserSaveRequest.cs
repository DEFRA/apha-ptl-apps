namespace PTL.Contracts.LabUsers;

/// <summary>Request body for adding a lab user to a lab and for editing one.</summary>
public sealed record LabUserSaveRequest(string Name, string Email, bool IsActive)
{
    /// <summary>The longest name the database holds.</summary>
    public const int MaxNameLength = 255;

    /// <summary>The longest valid email address (RFC 5321).</summary>
    public const int MaxEmailLength = 254;
}
