using System.Net;

namespace PTL.ApiClient.Tests.LoginInstructions;

public class LoginInstructionsApiClientTests
{
    private static (LoginInstructionsApiClient Client, List<HttpRequestMessage> Requests) CreateClient(HttpStatusCode statusCode, string? jsonContent)
    {
        var handler = new StubHttpMessageHandler(statusCode, jsonContent);
        var requests = new List<HttpRequestMessage>();
        handler.OnRequest = requests.Add;
        return (new LoginInstructionsApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), requests);
    }

    [Fact]
    public async Task SendToParticipantAsync_PostsToTheParticipantEndpointAndReturnsTheResponse()
    {
        var id = Guid.NewGuid();
        var (client, requests) = CreateClient(HttpStatusCode.OK, """{"success":true,"message":null}""");

        var result = await client.SendToParticipantAsync(id);

        Assert.True(result.Success);
        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/login-instructions/participants/{id}", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SendToViewerAsync_PostsToTheViewerEndpoint()
    {
        var id = Guid.NewGuid();
        var (client, requests) = CreateClient(HttpStatusCode.OK, """{"success":true}""");

        await client.SendToViewerAsync(id);

        Assert.Equal($"/api/login-instructions/viewers/{id}", Assert.Single(requests).RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SendToExternalTestConsultantAsync_PostsToTheConsultantEndpoint()
    {
        var id = Guid.NewGuid();
        var (client, requests) = CreateClient(HttpStatusCode.OK, """{"success":true}""");

        await client.SendToExternalTestConsultantAsync(id);

        Assert.Equal($"/api/login-instructions/external-test-consultants/{id}", Assert.Single(requests).RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Send_RefusedByTheApi_ReturnsTheReason()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, """{"success":false,"message":"There is no email address saved to send the login instructions to."}""");

        var result = await client.SendToViewerAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("There is no email address saved to send the login instructions to.", result.Message);
    }

    [Theory]
    [InlineData("participant")]
    [InlineData("viewer")]
    [InlineData("consultant")]
    public async Task Send_NotFound_ReturnsAFailureNamingTheRecordType(string kind)
    {
        var (client, _) = CreateClient(HttpStatusCode.NotFound, null);

        var result = kind switch
        {
            "participant" => await client.SendToParticipantAsync(Guid.NewGuid()),
            "viewer" => await client.SendToViewerAsync(Guid.NewGuid()),
            _ => await client.SendToExternalTestConsultantAsync(Guid.NewGuid()),
        };

        Assert.False(result.Success);
        Assert.Contains("was not found", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_EmptyBody_ReturnsAGenericFailure()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, "null");

        var result = await client.SendToViewerAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task Send_ServerError_Throws()
    {
        var (client, _) = CreateClient(HttpStatusCode.InternalServerError, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendToViewerAsync(Guid.NewGuid()));
    }
}
