namespace PTL.InternalWeb.Features.SystemAdministration;

// Resolves the RBAC decision confirmed Oct 2026: internal role membership stays in PT-LIMS's own
// database (tblRoles/tlnkUserRoles), not Entra ID groups - Entra only proves who signed in. This
// policy checks the "Admin" role already resolved into the ResolvedRoles claim by
// InternalUserResolver at sign-in time, not any Entra group/app-role.
public static class SystemAdministrationPolicy
{
    public const string Name = "SystemAdministration";
    public const string AdminRoleName = "Admin";
}
