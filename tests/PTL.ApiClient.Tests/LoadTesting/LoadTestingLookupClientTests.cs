using System.Net;
using System.Text.Json;
using PTL.Contracts.LoadTesting;

namespace PTL.ApiClient.Tests.LoadTesting;

public class LoadTestingLookupClientTests
{
    private static readonly Guid StoredId = Guid.Parse("321cbd77-fec2-f111-aaad-7c1e52845f5a");

    private static (LoadTestingLookupClient Client, List<HttpRequestMessage> Sent, List<string> Bodies) Create(HttpStatusCode statusCode, string? json)
    {
        var sent = new List<HttpRequestMessage>();
        var bodies = new List<string>();
        var handler = new StubHttpMessageHandler(statusCode, json)
        {
            OnRequest = request =>
            {
                sent.Add(request);
                bodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            }
        };

        return (new LoadTestingLookupClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }), sent, bodies);
    }

    [Fact]
    public async Task LookupInternalUserAsync_PostsTheUsernameWithTheSwitchHeaderAndReturnsTheResponse()
    {
        var (client, sent, bodies) = Create(HttpStatusCode.OK, $$"""{"status":"Found","ssoIdInt":"{{StoredId}}"}""");

        var result = await client.LookupInternalUserAsync(@"DEFRA\tester", CancellationToken.None);

        Assert.Equal(new LoadTestingInternalLookupResponse(LoadTestingInternalLookupStatus.Found, StoredId), result);
        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/load-testing/internal-users/lookup", request.RequestUri!.AbsolutePath);
        Assert.Equal(["true"], request.Headers.GetValues(LoadTestingHeaders.Enabled));
        Assert.Equal(@"DEFRA\tester", JsonElement.Parse(Assert.Single(bodies)).GetProperty("username").GetString());
    }

    [Fact]
    public async Task LookupExternalUserAsync_PostsTheEmailWithTheSwitchHeaderAndReturnsTheResponse()
    {
        var (client, sent, bodies) = Create(HttpStatusCode.OK, """{"status":"Ambiguous","ssoIdExt":null}""");

        var result = await client.LookupExternalUserAsync("tester@example.test", CancellationToken.None);

        Assert.Equal(new LoadTestingLookupResponse(LoadTestingLookupStatus.Ambiguous, null), result);
        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/load-testing/external-users/lookup", request.RequestUri!.AbsolutePath);
        Assert.Equal(["true"], request.Headers.GetValues(LoadTestingHeaders.Enabled));
        Assert.Equal("tester@example.test", JsonElement.Parse(Assert.Single(bodies)).GetProperty("email").GetString());
    }

    [Fact]
    public async Task Lookup_WhenPtlApiHasNoLookup_ReturnsNull()
    {
        var (client, _, _) = Create(HttpStatusCode.NotFound, null);

        Assert.Null(await client.LookupInternalUserAsync(@"DEFRA\tester", CancellationToken.None));
        Assert.Null(await client.LookupExternalUserAsync("tester@example.test", CancellationToken.None));
    }

    [Fact]
    public async Task Lookup_EmptyJsonBody_ReturnsNull()
    {
        var (client, _, _) = Create(HttpStatusCode.OK, "null");

        Assert.Null(await client.LookupExternalUserAsync("tester@example.test", CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Lookup_OtherFailureStatus_Throws(HttpStatusCode statusCode)
    {
        var (client, _, _) = Create(statusCode, null);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.LookupInternalUserAsync(@"DEFRA\tester", CancellationToken.None));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.LookupExternalUserAsync("tester@example.test", CancellationToken.None));
    }

    [Fact]
    public async Task Lookup_MalformedJsonBody_Throws()
    {
        var (client, _, _) = Create(HttpStatusCode.OK, "not json");

        await Assert.ThrowsAsync<JsonException>(() => client.LookupInternalUserAsync(@"DEFRA\tester", CancellationToken.None));
    }

    [Fact]
    public async Task Lookup_TransportFailure_Propagates()
    {
        var handler = new StubHttpMessageHandler(() => throw new HttpRequestException("connection refused"));
        var client = new LoadTestingLookupClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") });

        await Assert.ThrowsAsync<HttpRequestException>(() => client.LookupExternalUserAsync("tester@example.test", CancellationToken.None));
    }
}
