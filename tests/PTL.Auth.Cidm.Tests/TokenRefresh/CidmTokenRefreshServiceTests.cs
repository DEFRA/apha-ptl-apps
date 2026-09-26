using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PTL.Auth.Cidm.Options;
using PTL.Auth.Cidm.TokenRefresh;

namespace PTL.Auth.Cidm.Tests.TokenRefresh;

public class CidmTokenRefreshServiceTests
{
    private static readonly CidmOptions TestCidmOptions = new()
    {
        Address = "https://cidm.test/idphub/b2c",
        Policy = "b2c_1a_test_signupsignin",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        ServiceId = "service-id-value"
    };

    private static CidmTokenRefreshService CreateService(FakeHttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(CidmTokenRefreshService.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        var provider = services.BuildServiceProvider();

        var oidcOptions = new OpenIdConnectOptions
        {
            ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                new OpenIdConnectConfiguration { TokenEndpoint = "https://cidm.test/oauth2/v2.0/token" })
        };

        return new CidmTokenRefreshService(
            provider.GetRequiredService<IHttpClientFactory>(),
            new FakeOptionsMonitor<OpenIdConnectOptions>(oidcOptions),
            Microsoft.Extensions.Options.Options.Create(TestCidmOptions));
    }

    [Fact]
    public async Task RefreshAsync_SuccessfulResponse_ReturnsNewTokens()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(new
            {
                access_token = "new-access-token",
                id_token = "new-id-token",
                refresh_token = "new-refresh-token",
                expires_in = 3600
            })
        });
        var service = CreateService(handler);

        var result = await service.RefreshAsync("old-refresh-token", "https://app.test/signin-oidc");

        Assert.True(result.Succeeded);
        Assert.Equal("new-access-token", result.AccessToken);
        Assert.Equal("new-id-token", result.IdToken);
        Assert.Equal("new-refresh-token", result.RefreshToken);
        Assert.NotNull(result.ExpiresAt);
    }

    [Fact]
    public async Task RefreshAsync_NonSuccessStatusCode_ReturnsFailed()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent(new { error = "invalid_grant" })
        });
        var service = CreateService(handler);

        var result = await service.RefreshAsync("expired-refresh-token", "https://app.test/signin-oidc");

        Assert.False(result.Succeeded);
        Assert.Null(result.AccessToken);
    }

    [Fact]
    public async Task RefreshAsync_ResponseMissingAccessToken_ReturnsFailed()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(new { token_type = "Bearer" })
        });
        var service = CreateService(handler);

        var result = await service.RefreshAsync("old-refresh-token", "https://app.test/signin-oidc");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RefreshAsync_SendsExpectedFormFields()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedRequest = req;
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(new { access_token = "a", expires_in = 60 })
            };
        });
        var service = CreateService(handler);

        await service.RefreshAsync("the-refresh-token", "https://app.test/signin-oidc");

        Assert.Equal("https://cidm.test/oauth2/v2.0/token", capturedRequest!.RequestUri!.ToString());
        Assert.Contains("grant_type=refresh_token", capturedBody);
        Assert.Contains("refresh_token=the-refresh-token", capturedBody);
        Assert.Contains("client_id=client-id", capturedBody);
        Assert.Contains("redirect_uri=https%3A%2F%2Fapp.test%2Fsignin-oidc", capturedBody);
    }

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json");

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class FakeOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<T, string> listener) => null!;
    }
}
