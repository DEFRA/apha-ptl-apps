namespace PTL.Auth.Cidm.Claims;

/// <summary>
/// Parsed form of one "relationships" array entry:
/// relationshipId:organisationId:organisationName:organisationLoa:relationship:relationshipLoa
/// </summary>
public sealed record RelationshipInfo(
    string RelationshipId,
    string OrganisationId,
    string OrganisationName,
    int OrganisationLoa,
    string RelationshipType,
    int RelationshipLoa);
