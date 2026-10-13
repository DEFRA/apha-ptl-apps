namespace PTL.Core.User;

// Keyless domain projection for spgaRole (tblRoles) - the fixed list of internal application
// roles. Named "Name" (not "Role") to avoid CS0542 (a member can't share its enclosing type's name).
public sealed class Role
{
    public Guid RoleId { get; set; }
    public string Name { get; set; } = string.Empty;
}
