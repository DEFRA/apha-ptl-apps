namespace PTL.Auth.Cidm.Claims;

/// <summary>
/// Parsed form of one "roles" array entry: relationshipId:roleName:status.
/// </summary>
/// <remarks>
/// <see cref="Status"/> is kept as the raw numeric value from CIDM rather than an enum. The onboarding
/// guide lists 7 status names (Incomplete, Pending, Complete (approved), Complete (Rejected), Blocked,
/// Access Removed, Offboarded) but never states which integer maps to which - this has not been confirmed
/// with the DEFRA CIDM team, so no authorization decision should be based on an assumed mapping.
/// </remarks>
public sealed record RoleInfo(string RelationshipId, string RoleName, int Status);
