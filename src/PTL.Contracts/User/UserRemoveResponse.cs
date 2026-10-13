namespace PTL.Contracts.User;

// Success = false (with Message populated) when removal is blocked by a business rule (e.g.
// removing your own account) - a legitimate outcome, not a malformed request, so this is
// returned with 200 OK rather than 400, matching SetUserRolesResponse/CountryDeleteResponse.
public sealed record UserRemoveResponse(bool Success, string? Message);
