namespace PTL.Contracts.User;

// Success = false (with Message populated) when the change is blocked by a business rule (e.g.
// removing your own Admin role) - a legitimate outcome, not a malformed request, so this is
// returned with 200 OK rather than 400.
public sealed record SetUserRolesResponse(bool Success, string? Message);
