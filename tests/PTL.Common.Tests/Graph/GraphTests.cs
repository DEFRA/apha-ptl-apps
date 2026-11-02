using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PTL.Common.Graph;

namespace PTL.Common.Tests.Graph;

// Scripted HTTP layer: records every request and answers from a queue, so no network is involved.
internal sealed class ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
{
    private int _next;

    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Requests)
        {
            Requests.Add((request, body));
        }

        var index = Math.Min(Interlocked.Increment(ref _next) - 1, responses.Length - 1);
        return responses[index](request);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

internal sealed class FakeHttpClientFactory(HttpMessageHandler handler, string? baseAddress = null) : IHttpClientFactory
{
    public string? LastName { get; private set; }

    public HttpClient CreateClient(string name)
    {
        LastName = name;
        return new HttpClient(handler, disposeHandler: false) { BaseAddress = baseAddress is null ? null : new Uri(baseAddress) };
    }
}

internal sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

public class GraphTokenProviderTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    // Generated per run, so no secret-like literal sits in source control (and the tests can check it never leaks).
    private static readonly string FakeSecret = Guid.NewGuid().ToString("N");

    private static GraphOptions Configured() => new() { TenantId = "tenant-1", ClientId = "client-1", ClientSecret = FakeSecret };

    private static string Token(string value = "tok-1", int expiresIn = 3600) => $$"""{"access_token":"{{value}}","expires_in":{{expiresIn}}}""";

    private static (GraphTokenProvider Provider, ScriptedHandler Handler, TestTimeProvider Time) Create(
        GraphOptions? options = null, params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
    {
        var handler = new ScriptedHandler(responses.Length == 0 ? [_ => ScriptedHandler.Json(Token())] : responses);
        var time = new TestTimeProvider(Start);
        return (new GraphTokenProvider(new FakeHttpClientFactory(handler), Options.Create(options ?? Configured()), time), handler, time);
    }

    [Fact]
    public async Task GetAccessToken_PostsTheClientCredentialsGrantToTheTenantTokenEndpoint()
    {
        var (provider, handler, _) = Create();

        var token = await provider.GetAccessTokenAsync();

        Assert.Equal("tok-1", token);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://login.microsoftonline.com/tenant-1/oauth2/v2.0/token", request.RequestUri!.ToString());
        Assert.Contains("grant_type=client_credentials", body, StringComparison.Ordinal);
        Assert.Contains("client_id=client-1", body, StringComparison.Ordinal);
        Assert.Contains($"client_secret={FakeSecret}", body, StringComparison.Ordinal);
        Assert.Contains("scope=https%3A%2F%2Fgraph.microsoft.com%2F.default", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAccessToken_ReusesTheTokenUntilItIsCloseToExpiry()
    {
        var (provider, handler, time) = Create();

        await provider.GetAccessTokenAsync();
        time.Advance(TimeSpan.FromMinutes(54));
        await provider.GetAccessTokenAsync();

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetAccessToken_RequestsANewTokenOnceTheRefreshWindowIsReached()
    {
        var (provider, handler, time) = Create(null, _ => ScriptedHandler.Json(Token("tok-1")), _ => ScriptedHandler.Json(Token("tok-2")));

        await provider.GetAccessTokenAsync();
        time.Advance(TimeSpan.FromMinutes(56));
        var second = await provider.GetAccessTokenAsync();

        Assert.Equal("tok-2", second);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetAccessToken_ConcurrentCallersShareOneTokenRequest()
    {
        var (provider, handler, _) = Create();

        var tokens = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => provider.GetAccessTokenAsync()));

        Assert.All(tokens, t => Assert.Equal("tok-1", t));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetAccessToken_TokenShorterThanTheRefreshWindow_IsNotReused()
    {
        var (provider, handler, _) = Create(null, _ => ScriptedHandler.Json(Token(expiresIn: 60)));

        await provider.GetAccessTokenAsync();
        await provider.GetAccessTokenAsync();

        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData("", "client-1", "x")]
    [InlineData("tenant-1", " ", "x")]
    [InlineData("tenant-1", "client-1", "")]
    public async Task GetAccessToken_MissingCredential_ThrowsWithoutCallingOut(string tenant, string client, string secret)
    {
        var (provider, handler, _) = Create(new GraphOptions { TenantId = tenant, ClientId = client, ClientSecret = secret });

        var exception = await Assert.ThrowsAsync<GraphException>(() => provider.GetAccessTokenAsync());

        Assert.Contains("Graph:ClientSecret", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetAccessToken_FailureResponse_ThrowsWithTheStatusOnly()
    {
        var (provider, _, _) = Create(null, _ => ScriptedHandler.Json($$"""{"error":"invalid_client","error_description":"{{FakeSecret}} leaked"}""", HttpStatusCode.Unauthorized));

        var exception = await Assert.ThrowsAsync<GraphException>(() => provider.GetAccessTokenAsync());

        Assert.Contains("401", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeSecret, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAccessToken_ResponseWithoutAnAccessToken_Throws()
    {
        var (provider, _, _) = Create(null, _ => ScriptedHandler.Json("{}"));

        await Assert.ThrowsAsync<GraphException>(() => provider.GetAccessTokenAsync());
    }

    [Fact]
    public async Task GetAccessToken_AfterAFailure_TriesAgainNextTime()
    {
        var (provider, handler, _) = Create(null, _ => ScriptedHandler.Json("{}", HttpStatusCode.InternalServerError), _ => ScriptedHandler.Json(Token("tok-2")));

        await Assert.ThrowsAsync<GraphException>(() => provider.GetAccessTokenAsync());
        var token = await provider.GetAccessTokenAsync();

        Assert.Equal("tok-2", token);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void Dispose_CanBeCalled()
    {
        var (provider, _, _) = Create();

        provider.Dispose();
    }
}

public class GraphAuthorizationHandlerTests
{
    private sealed class FixedTokenProvider(string token) : IGraphTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(token);
    }

    [Fact]
    public async Task SendAsync_AddsTheBearerToken()
    {
        var inner = new ScriptedHandler(_ => ScriptedHandler.Json("{}"));
        using var client = new HttpClient(new GraphAuthorizationHandler(new FixedTokenProvider("abc")) { InnerHandler = inner });

        await client.GetAsync("https://graph.microsoft.com/v1.0/users");

        var (request, _) = Assert.Single(inner.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("abc", request.Headers.Authorization.Parameter);
    }
}

public class GraphOptionsTests
{
    [Fact]
    public void TokenEndpoint_UsesTheConfiguredAuthorityAndEscapesTheTenant()
    {
        var options = new GraphOptions { TenantId = "my tenant", LoginAuthority = "https://login.example.test/" };

        Assert.Equal("https://login.example.test/my%20tenant/oauth2/v2.0/token", options.TokenEndpoint);
    }

    [Fact]
    public void IsConfigured_IsFalseUntilAllThreeCredentialsAreSet() =>
        Assert.False(new GraphOptions { TenantId = "t", ClientId = "c" }.IsConfigured);
}

public class GraphServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGraphClient_BindsTheGraphSectionAndRegistersTheProviderAndNamedClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Graph:TenantId"] = "t", ["Graph:ClientId"] = "c", ["Graph:ClientSecret"] = "s" })
            .Build();
        var services = new ServiceCollection();

        services.AddGraphClient(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.True(provider.GetRequiredService<IOptions<GraphOptions>>().Value.IsConfigured);
        Assert.IsType<GraphTokenProvider>(provider.GetRequiredService<IGraphTokenProvider>());
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(GraphServiceCollectionExtensions.GraphHttpClientName);
        Assert.Equal("https://graph.microsoft.com/v1.0/", client.BaseAddress!.ToString());
    }

    [Fact]
    public void AddGraphClient_WithNoGraphSection_StillBuilds()
    {
        var services = new ServiceCollection();

        services.AddGraphClient(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<IOptions<GraphOptions>>().Value.IsConfigured);
    }

    [Fact]
    public void AddGraphClient_WithoutGraphCredentials_ReusesEntraCredentials()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Entra:TenantId"] = "t",
                ["Entra:ClientId"] = "c",
                ["Entra:ClientSecret"] = "s"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddGraphClient(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GraphOptions>>().Value;
        Assert.True(options.IsConfigured);
        Assert.Equal("t", options.TenantId);
        Assert.Equal("c", options.ClientId);
        Assert.Equal("s", options.ClientSecret);
    }
}
