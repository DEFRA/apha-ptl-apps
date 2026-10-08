namespace PTL.Auth.Cidm.Claims;

public static class CidmClaimTypes
{
    public const string CorrelationId = "correlationId";
    public const string SessionId = "sessionId";
    public const string ContactId = "contactId";
    public const string ServiceId = "serviceId";
    public const string UniqueReference = "uniqueReference";
    public const string LevelOfAssurance = "loa";
    public const string AuthenticationAssuranceLevel = "aal";
    public const string EnrolmentCount = "enrolmentCount";
    public const string EnrolmentRequestCount = "enrolmentRequestCount";
    public const string CurrentRelationshipId = "currentRelationshipId";

    /// <summary>Raw claim name for each "relationships" array entry, before parsing.</summary>
    public const string RawRelationships = "relationships";

    /// <summary>Raw claim name for each "roles" array entry, before parsing.</summary>
    public const string RawRoles = "roles";

    /// <summary>Claim type used to store each parsed <see cref="RelationshipInfo"/> (as JSON) on the principal.</summary>
    public const string Relationship = "cidm:relationship";

    /// <summary>Claim type used to store each parsed <see cref="RoleInfo"/> (as JSON) on the principal.</summary>
    public const string Role = "cidm:role";
}
