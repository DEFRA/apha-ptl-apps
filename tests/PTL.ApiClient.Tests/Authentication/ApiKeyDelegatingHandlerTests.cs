using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PTL.ApiClient.Authentication;
using PTL.Common.ApiAuthentication;

namespace PTL.ApiClient.Tests.Authentication;

public class ApiKeyDelegatingHandlerTests
{
    private const string ApiBaseUrl = "http://ptl-api:8080";
    private const string ApiKey = "test-api-key";

    private static async Task<HttpRequestMessage> SendThroughHandlerAsync(HttpRequestMessage request, string clientApp = ApiAuthenticationOptions.InternalWeb)
    {
        HttpRequestMessage? sent = null;
        var handler = new ApiKeyDelegatingHandler(
            new Uri(ApiBaseUrl),
            Options.Create(new ApiAuthenticationOptions { ApiKey = ApiKey, ClientApp = clientApp }))
        {
            InnerHandler = new StubHttpMessageHandler(HttpStatusCode.OK, null) { OnRequest = r => sent = r }
        };

        using var invoker = new HttpMessageInvoker(handler);
        await invoker.SendAsync(request, CancellationToken.None);
        return sent!;
    }

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    [Theory]
    [InlineData(ApiAuthenticationOptions.InternalWeb)]
    [InlineData(ApiAuthenticationOptions.ExternalWeb)]
    public async Task ApiRequest_CarriesTheApiKeyAndClientApp(string clientApp)
    {
        var sent = await SendThroughHandlerAsync(new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/api/customers"), clientApp);

        Assert.Equal(ApiKey, Header(sent, ApiAuthenticationOptions.ApiKeyHeaderName));
        Assert.Equal(clientApp, Header(sent, ApiAuthenticationOptions.ClientAppHeaderName));
    }

    [Theory]
    [InlineData("https://graph.microsoft.com/v1.0/me")]
    [InlineData("http://ptl-api:9090/api/customers")]
    [InlineData("https://ptl-api:8080/api/customers")]
    [InlineData("http://ptl-api.evil.example:8080/api/customers")]
    public async Task RequestToAnyOtherHost_NeverCarriesTheApiKey(string url)
    {
        var sent = await SendThroughHandlerAsync(new HttpRequestMessage(HttpMethod.Get, url));

        Assert.Null(Header(sent, ApiAuthenticationOptions.ApiKeyHeaderName));
        Assert.Null(Header(sent, ApiAuthenticationOptions.ClientAppHeaderName));
    }

    [Fact]
    public async Task ApiRequest_ReplacesAnyHeadersAlreadySet()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/api/customers");
        request.Headers.Add(ApiAuthenticationOptions.ApiKeyHeaderName, "spoofed");
        request.Headers.Add(ApiAuthenticationOptions.ClientAppHeaderName, "Spoofed");

        var sent = await SendThroughHandlerAsync(request);

        Assert.Equal(ApiKey, Header(sent, ApiAuthenticationOptions.ApiKeyHeaderName));
        Assert.Equal(ApiAuthenticationOptions.InternalWeb, Header(sent, ApiAuthenticationOptions.ClientAppHeaderName));
    }

    [Fact]
    public async Task AddPtlApiClient_StampsEveryApiClientButNoOtherHttpClient()
    {
        var captured = new List<HttpRequestMessage>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPtlApiClient(Configuration(ApiKey, ApiAuthenticationOptions.ExternalWeb));
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() =>
            new StubHttpMessageHandler(HttpStatusCode.OK, null) { OnRequest = captured.Add }));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        await factory.CreateClient(nameof(ICustomerApiClient)).GetAsync(new Uri("/api/customers", UriKind.Relative));
        await factory.CreateClient("Graph").GetAsync(new Uri("https://graph.microsoft.com/v1.0/me"));

        Assert.Equal(ApiKey, Header(captured[0], ApiAuthenticationOptions.ApiKeyHeaderName));
        Assert.Equal(ApiAuthenticationOptions.ExternalWeb, Header(captured[0], ApiAuthenticationOptions.ClientAppHeaderName));
        Assert.Null(Header(captured[1], ApiAuthenticationOptions.ApiKeyHeaderName));
    }

    [Theory]
    [InlineData(null, ApiAuthenticationOptions.InternalWeb, "ApiAuthentication:ApiKey")]
    [InlineData("   ", ApiAuthenticationOptions.InternalWeb, "ApiAuthentication:ApiKey")]
    [InlineData(ApiKey, null, "ApiAuthentication:ClientApp")]
    [InlineData(ApiKey, "internalweb", "ApiAuthentication:ClientApp")]
    [InlineData(ApiKey, "PTL.Api", "ApiAuthentication:ClientApp")]
    public void AddPtlApiClient_RejectsMissingOrInvalidSettings(string? apiKey, string? clientApp, string expectedKey)
    {
        var services = new ServiceCollection();
        services.AddPtlApiClient(Configuration(apiKey, clientApp));
        using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ApiAuthenticationOptions>>().Value);

        Assert.Contains(expectedKey, ex.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(string? apiKey, string? clientApp) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:BaseUrl"] = ApiBaseUrl,
                ["ApiAuthentication:ApiKey"] = apiKey,
                ["ApiAuthentication:ClientApp"] = clientApp,
            })
            .Build();
}
