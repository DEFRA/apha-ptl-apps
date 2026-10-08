namespace PTL.Core.ExternalUser;

/// <summary>
/// Resolves a CIDM-authenticated external user against the Participant, Viewer and Test
/// Consultant tables, independently for each CIDM role the user holds.
/// </summary>
public interface IExternalUserService
{
    /// <summary>
    /// For each recognised CIDM role name in <paramref name="cidmRoles"/> ("Participant",
    /// "Viewer", "Test Consultant"), looks up the matching record by SsoIdExt, falling back to a
    /// match by email (backfilling SsoIdExt when found). Roles that do not resolve are omitted
    /// from the result - no record is ever auto-created.
    /// </summary>
    Task<ExternalUserResolutionResult> ResolveAsync(
        Guid ssoIdExt,
        string email,
        string displayName,
        IReadOnlyList<string> cidmRoles,
        CancellationToken cancellationToken = default);
}
