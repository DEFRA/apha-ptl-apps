using System.Net;
using PTL.ApiClient;
using PTL.ApiClient.Tests;

namespace PTL.ApiClient.Tests.User;

public class RoleApiClientTests
{
    private static RoleApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task GetRolesAsync_Success_ReturnsDeserializedRoles()
    {
        const string json = """[{"roleId":"11111111-1111-1111-1111-111111111111","name":"Admin"}]""";
        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetRolesAsync();

        Assert.Equal("Admin", Assert.Single(result).Name);
    }

    [Fact]
    public async Task GetRolesAsync_NullResponse_ReturnsEmpty()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.GetRolesAsync();

        Assert.Empty(result);
    }
}
