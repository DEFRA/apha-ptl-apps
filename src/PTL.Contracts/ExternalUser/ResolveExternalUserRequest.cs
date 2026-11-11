namespace PTL.Contracts.ExternalUser;

/// <summary>Request body for <c>POST /api/external-users/resolve</c>. The role comes from the matched PTL record, never from the caller.</summary>
public sealed record ResolveExternalUserRequest(
    Guid SsoIdExt,
    string Email,
    string DisplayName)
{
    /// <summary>The longest valid email address (RFC 5321).</summary>
    public const int MaxEmailLength = 254;

    /// <summary>The longest display name accepted.</summary>
    public const int MaxDisplayNameLength = 200;
}
