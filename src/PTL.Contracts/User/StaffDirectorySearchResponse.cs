namespace PTL.Contracts.User;

// Response of GET /api/users/directory-search. IsTruncated is true when more people matched than
// the search returns, so the caller can ask the user to narrow the search.
public sealed record StaffDirectorySearchResponse(IReadOnlyList<StaffDirectoryUserResponse> Users, bool IsTruncated);
