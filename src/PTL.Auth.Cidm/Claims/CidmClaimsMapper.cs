using System.Globalization;

namespace PTL.Auth.Cidm.Claims;

public static class CidmClaimsMapper
{
    public static IReadOnlyList<RelationshipInfo> ParseRelationships(IEnumerable<string> rawRelationships)
    {
        var results = new List<RelationshipInfo>();
        foreach (var raw in rawRelationships)
        {
            if (TryParseRelationship(raw, out var relationship))
            {
                results.Add(relationship);
            }
        }

        return results;
    }

    public static bool TryParseRelationship(string raw, out RelationshipInfo relationship)
    {
        var parts = raw.Split(':');
        if (parts.Length == 6 &&
            int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var organisationLoa) &&
            int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var relationshipLoa))
        {
            relationship = new RelationshipInfo(parts[0], parts[1], parts[2], organisationLoa, parts[4], relationshipLoa);
            return true;
        }

        relationship = null!;
        return false;
    }

    public static IReadOnlyList<RoleInfo> ParseRoles(IEnumerable<string> rawRoles)
    {
        var results = new List<RoleInfo>();
        foreach (var raw in rawRoles)
        {
            if (TryParseRole(raw, out var role))
            {
                results.Add(role);
            }
        }

        return results;
    }

    public static bool TryParseRole(string raw, out RoleInfo role)
    {
        var parts = raw.Split(':');
        if (parts.Length == 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var status))
        {
            role = new RoleInfo(parts[0], parts[1], status);
            return true;
        }

        role = null!;
        return false;
    }
}
