using System.Net;
using PTL.ApiClient.Tests;
using PTL.Contracts.ExternalUser;

namespace PTL.ApiClient.Tests.ExternalUser;

public class ExternalUserApiClientTests
{
    private static ExternalUserApiClient CreateClient(HttpStatusCode statusCode, string? jsonContent) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, jsonContent)) { BaseAddress = new Uri("http://localhost") });

    [Fact]
    public async Task ResolveAsync_ReturnsDeserializedResponse()
    {
        const string json = """{"displayName":"Jane Doe","roles":["Viewer"],"participantId":null,"viewerId":"22222222-2222-2222-2222-222222222222","testConsultantId":null}""";
        var client = CreateClient(HttpStatusCode.OK, json);
        var request = new ResolveExternalUserRequest(Guid.NewGuid(), "jane@example.com", "Jane Doe", ["Viewer"]);

        var result = await client.ResolveAsync(request);

        Assert.Equal("Jane Doe", result.DisplayName);
        Assert.Equal(["Viewer"], result.Roles);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), result.ViewerId);
        Assert.Null(result.ParticipantId);
    }

    [Fact]
    public async Task ResolveAsync_NullResponse_ReturnsFallbackWithRequestDisplayName()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");
        var request = new ResolveExternalUserRequest(Guid.NewGuid(), "jane@example.com", "Jane Doe", ["Viewer"]);

        var result = await client.ResolveAsync(request);

        Assert.Equal("Jane Doe", result.DisplayName);
        Assert.Empty(result.Roles);
        Assert.Null(result.ParticipantId);
        Assert.Null(result.ViewerId);
        Assert.Null(result.TestConsultantId);
    }

    [Fact]
    public async Task ResolveAsync_ErrorStatusCode_Throws()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, null);
        var request = new ResolveExternalUserRequest(Guid.NewGuid(), "jane@example.com", "Jane Doe", ["Viewer"]);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ResolveAsync(request));
    }
}
