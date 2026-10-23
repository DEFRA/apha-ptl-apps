namespace PTL.Core.User;

// IsTruncated is true when more people matched than the search was willing to return, so callers
// can ask the user to refine the search rather than silently dropping results.
public sealed record StaffDirectorySearchResult(IReadOnlyList<StaffDirectoryUser> Users, bool IsTruncated);
