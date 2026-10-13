namespace PTL.Core.User;

// Legacy UserCreate.aspx.vb queried on-prem Active Directory directly via
// System.DirectoryServices.DirectorySearcher, with ADdomain/ADuser/ADpassword read from plaintext
// Web.config app settings. That has no equivalent once internal auth moves to Entra ID (OIDC) -
// see docs/migration/System-Administration-migration.md "Authentication Mapping" and the HLD's
// Role/Claim mapping section. The real replacement (Microsoft Graph directory search, most
// likely) is a decision for the separate identity migration workstream, not this feature.
public interface IStaffDirectoryService
{
    Task<IReadOnlyList<StaffDirectoryUser>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
}
