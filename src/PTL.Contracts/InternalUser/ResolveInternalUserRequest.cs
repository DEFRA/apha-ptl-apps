namespace PTL.Contracts.InternalUser;

/// <summary>Request body for <c>POST /api/internal-users/resolve</c>.</summary>
public sealed record ResolveInternalUserRequest(Guid SsoIdInt, string Username);
