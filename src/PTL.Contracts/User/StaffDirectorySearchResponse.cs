namespace PTL.Contracts.User;

// The Create User search result PTL.InternalWeb shows (the search itself runs there, against Microsoft Graph).
// IsTruncated is true when more people matched than the search returns, so the user is asked to narrow the search.
public sealed record StaffDirectorySearchResponse(IReadOnlyList<StaffDirectoryUserResponse> Users, bool IsTruncated);
