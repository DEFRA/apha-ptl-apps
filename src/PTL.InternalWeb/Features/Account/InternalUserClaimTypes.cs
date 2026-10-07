namespace PTL.InternalWeb.Features.Account;

/// <summary>Claim types added to the principal once <see cref="InternalUserResolver"/> resolves it.</summary>
public static class InternalUserClaimTypes
{
    /// <summary>The resolved tblUsers.fldUserId, as a string GUID.</summary>
    public const string InternalUserId = "internalUserId";

    /// <summary>The resolved display name (fldFriendlyName, falling back to first+last name).</summary>
    public const string FullName = "fullName";

    /// <summary>The resolved tblUsers.fldDepartment.</summary>
    public const string Department = "department";

    /// <summary>
    /// Comma-separated list of the user's tblRoles role names (including the synthetic
    /// "Distributions" role where applicable) - a single claim value, not one claim per role,
    /// matching the pattern used for PT-LIMS's external-user resolved roles.
    /// </summary>
    public const string ResolvedRoles = "resolvedRoles";
}
