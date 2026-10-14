namespace PTL.Common.Audit;

/// <summary>
/// How the signed-in internal user's identity travels from PTL.InternalWeb to PTL.Api, so
/// System Administration changes can be audited against the person who made them.
/// </summary>
public static class ActingUser
{
    /// <summary>The claim PTL.InternalWeb adds at sign-in holding tblUsers.fldUserId.</summary>
    public const string ClaimType = "internalUserId";

    /// <summary>The request header PTL.InternalWeb forwards that value in.</summary>
    public const string HeaderName = "X-Acting-User-Id";
}
