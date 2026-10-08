namespace PTL.Contracts.User;

// Result of searching the staff directory (legacy on-prem AD via DirectorySearcher) for a person
// to onboard - see UserController's [NEEDS INVESTIGATION] note on the stubbed directory source.
public sealed record StaffDirectoryUserResponse(string Username, string Email, string FriendlyName, string FirstName, string LastName);
