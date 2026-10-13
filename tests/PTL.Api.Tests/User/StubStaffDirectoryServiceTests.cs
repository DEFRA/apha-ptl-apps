using PTL.Core.User;

namespace PTL.Api.Tests.User;

public class StubStaffDirectoryServiceTests
{
    [Theory]
    [InlineData("m100001")]
    [InlineData("Jane")]
    [InlineData("Smith")]
    public async Task SearchAsync_MatchesUsernameForenameOrSurname(string term)
    {
        var service = new StubStaffDirectoryService();

        var results = await service.SearchAsync(term);

        Assert.Contains(results, r => r.Username == "m100001");
    }

    [Fact]
    public async Task SearchAsync_NoMatch_ReturnsEmpty()
    {
        var service = new StubStaffDirectoryService();

        var results = await service.SearchAsync("nonexistent-term-xyz");

        Assert.Empty(results);
    }
}
