namespace PTL.ExternalWeb.Features.Account;

// Must match PTL.Core.ExternalUser.ExternalUserService's private role-name constants exactly -
// those are what ends up in the ResolvedRoles claim this class's consumers compare against.
public static class ExternalRoleNames
{
    public const string Participant = "Participant";
    public const string Viewer = "Viewer";
    public const string TestConsultant = "Test Consultant";
}
