using PTL.Auth.Cidm.Claims;

namespace PTL.Auth.Cidm.Tests.Claims;

public class CidmClaimsMapperTests
{
    private const string ValidRelationship =
        "23950a2d-c37d-43da-9fcb-0a4ce9aa11ee:bc19305a-f9b6-ea11-a812-000d3ab4653d:ABC Organisation2:0:Employee:0";
    private const string ValidRole = "23950a2d-c37d-43da-9fcb-0a4ce9aa11ee:Certifier:3";

    [Fact]
    public void TryParseRelationship_ValidString_ParsesAllFields()
    {
        var parsed = CidmClaimsMapper.TryParseRelationship(ValidRelationship, out var relationship);

        Assert.True(parsed);
        Assert.Equal("23950a2d-c37d-43da-9fcb-0a4ce9aa11ee", relationship.RelationshipId);
        Assert.Equal("bc19305a-f9b6-ea11-a812-000d3ab4653d", relationship.OrganisationId);
        Assert.Equal("ABC Organisation2", relationship.OrganisationName);
        Assert.Equal(0, relationship.OrganisationLoa);
        Assert.Equal("Employee", relationship.RelationshipType);
        Assert.Equal(0, relationship.RelationshipLoa);
    }

    [Theory]
    [InlineData("too:few:parts")]
    [InlineData("a:b:c:notanumber:Employee:0")]
    [InlineData("a:b:c:0:Employee:notanumber")]
    [InlineData("")]
    public void TryParseRelationship_MalformedString_ReturnsFalse(string raw)
    {
        var parsed = CidmClaimsMapper.TryParseRelationship(raw, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void ParseRelationships_SkipsMalformedEntriesAndKeepsValidOnes()
    {
        var result = CidmClaimsMapper.ParseRelationships([ValidRelationship, "malformed"]);

        Assert.Single(result);
        Assert.Equal("ABC Organisation2", result[0].OrganisationName);
    }

    [Fact]
    public void ParseRelationships_EmptyInput_ReturnsEmpty()
    {
        var result = CidmClaimsMapper.ParseRelationships([]);

        Assert.Empty(result);
    }

    [Fact]
    public void TryParseRole_ValidString_ParsesAllFields()
    {
        var parsed = CidmClaimsMapper.TryParseRole(ValidRole, out var role);

        Assert.True(parsed);
        Assert.Equal("23950a2d-c37d-43da-9fcb-0a4ce9aa11ee", role.RelationshipId);
        Assert.Equal("Certifier", role.RoleName);
        Assert.Equal(3, role.Status);
    }

    [Theory]
    [InlineData("too:few")]
    [InlineData("a:b:notanumber")]
    [InlineData("")]
    public void TryParseRole_MalformedString_ReturnsFalse(string raw)
    {
        var parsed = CidmClaimsMapper.TryParseRole(raw, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void ParseRoles_SkipsMalformedEntriesAndKeepsValidOnes()
    {
        var result = CidmClaimsMapper.ParseRoles([ValidRole, "malformed:x"]);

        Assert.Single(result);
        Assert.Equal("Certifier", result[0].RoleName);
    }

    [Fact]
    public void ParseRoles_MultipleValidEntries_ParsesAll()
    {
        var result = CidmClaimsMapper.ParseRoles([ValidRole, "23950a2d-c37d-43da-9fcb-0a4ce9aa11ee:Reviewer:3"]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.RoleName == "Certifier");
        Assert.Contains(result, r => r.RoleName == "Reviewer");
    }
}
