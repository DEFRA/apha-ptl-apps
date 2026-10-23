namespace PTL.Contracts.User;

// Result of searching the staff directory (Microsoft Graph, replacing legacy's on-prem AD DirectorySearcher)
// for a person to onboard.
public sealed record StaffDirectoryUserResponse(string Username, string Email, string FriendlyName, string FirstName, string LastName);
