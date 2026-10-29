namespace PTL.Core.User;

// Legacy UserCreate.aspx.vb queried on-prem Active Directory directly via
// System.DirectoryServices.DirectorySearcher, with ADdomain/ADuser/ADpassword read from plaintext
// Web.config app settings. The replacement is a Microsoft Graph user search run by PTL.InternalWeb
// (Infrastructure/GraphStaffDirectoryService), authenticated with the "Graph" app registration.
public interface IStaffDirectoryService
{
    Task<StaffDirectorySearchResult> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
}
