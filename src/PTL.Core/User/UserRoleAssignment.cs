namespace PTL.Core.User;

// Keyless domain projection for spgUserRoleList (tlnkUserRoles) - one row-to-role link.
public sealed class UserRoleAssignment
{
    public Guid UserRoleId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}
