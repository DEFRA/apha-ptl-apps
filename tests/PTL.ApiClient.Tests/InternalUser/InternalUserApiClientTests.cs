using System.Net;
using PTL.ApiClient.Tests;
using PTL.Contracts.InternalUser;

namespace PTL.ApiClient.Tests.InternalUser;

public class InternalUserApiClientTests
{
    private static InternalUserApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task ResolveAsync_ReturnsDeserializedResponse()
    {
        const string json = """{"isPermitted":true,"userId":"22222222-2222-2222-2222-222222222222","fullName":"Alice User","department":"QAU","roles":["Admin"]}""";
        var client = CreateClient(HttpStatusCode.OK, json);
        var request = new ResolveInternalUserRequest(Guid.NewGuid(), "DEFRA\\auser");

        var result = await client.ResolveAsync(request);

        Assert.True(result.IsPermitted);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), result.UserId);
        Assert.Equal("Alice User", result.FullName);
        Assert.Equal(["Admin"], result.Roles);
    }

    [Fact]
    public async Task ResolveAsync_NullResponse_ReturnsDeniedFallback()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");
        var request = new ResolveInternalUserRequest(Guid.NewGuid(), "DEFRA\\auser");

        var result = await client.ResolveAsync(request);

        Assert.False(result.IsPermitted);
        Assert.Null(result.UserId);
        Assert.Empty(result.Roles);
    }

    [Fact]
    public async Task ResolveAsync_ErrorStatusCode_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);
        var request = new ResolveInternalUserRequest(Guid.NewGuid(), "DEFRA\\auser");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ResolveAsync(request));
    }
}
