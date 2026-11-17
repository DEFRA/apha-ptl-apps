using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PTL.ApiClient;
using PTL.Contracts.InternalUser;
using PTL.Contracts.LoadTesting;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.LoadTesting;

public partial class LoadTestingLoginTests(PtlInternalWebTestFactory factory) : IClassFixture<PtlInternalWebTestFactory>
{
    private static readonly Guid StoredObjectId = Guid.Parse("321cbd77-fec2-f111-aaad-7c1e52845f5a");

    private static readonly ResolveInternalUserResponse Resolved = new(true, Guid.NewGuid(), "Load Tester", "Labs", ["Admin"]);

    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenPattern();

    private sealed class FakeLookupClient(LoadTestingInternalLookupResponse? response) : ILoadTestingLookupClient
    {
        public string? ReceivedUsername { get; private set; }

        public Task<LoadTestingInternalLookupResponse?> LookupInternalUserAsync(string username, CancellationToken cancellationToken)
        {
            ReceivedUsername = username;
            return Task.FromResult(response);
        }

        public Task<LoadTestingLookupResponse?> LookupExternalUserAsync(string email, CancellationToken cancellationToken) =>
            throw new NotSupportedException("PTL.InternalWeb only looks up internal users.");
    }

    private static LoadTestingInternalLookupResponse Found => new(LoadTestingInternalLookupStatus.Found, StoredObjectId);

    private static WebApplicationFactory<Program> CreateApp(
        WebApplicationFactory<Program> source,
        bool enabled,
        ILoadTestingLookupClient? lookup = null,
        FakeInternalUserApiClient? api = null) =>
        source.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["LoadTesting:Enabled"] = enabled ? "true" : "false" }));

            builder.ConfigureServices(services =>
            {
                if (lookup is not null)
                {
                    services.AddSingleton(lookup);
                }

                if (api is not null)
                {
                    services.AddSingleton<IInternalUserApiClient>(api);
                }
            });
        });

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> source,
        bool enabled,
        ILoadTestingLookupClient? lookup = null,
        FakeInternalUserApiClient? api = null) =>
        CreateApp(source, enabled, lookup, api).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // Posts the form the way a browser (or a JMeter script) does: token and cookie from the rendered page.
    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string? username, string? returnUrl = null)
    {
        var page = await client.GetAsync("/perf/login");
        var token = AntiforgeryTokenPattern().Match(await page.Content.ReadAsStringAsync()).Groups[1].Value;
        var cookie = string.Join("; ", page.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));

        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["returnUrl"] = returnUrl ?? string.Empty };
        if (username is not null)
        {
            fields["username"] = username;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/perf/login") { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static (string Cookie, string Token) IssueAntiforgeryTokens(IServiceProvider services)
    {
        var tokens = services.GetRequiredService<IAntiforgery>().GetTokens(new DefaultHttpContext { RequestServices = services });
        var cookieName = services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie.Name;
        return ($"{cookieName}={tokens.CookieToken}", tokens.RequestToken!);
    }

    private static bool HasSessionCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
        && values.Any(cookie => cookie.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));

    [Fact]
    public async Task WhenDisabled_LoginPageAndPostAreNotFound()
    {
        var app = CreateApp(factory, enabled: false);
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (cookie, token) = IssueAntiforgeryTokens(app.Services);
        using var post = new HttpRequestMessage(HttpMethod.Post, "/perf/login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["username"] = @"DEFRA\a" })
        };
        post.Headers.Add("Cookie", cookie);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/perf/login")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(post)).StatusCode);
    }

    [Fact]
    public async Task WhenEnabled_PostWithoutAnAntiforgeryTokenIsRejectedBeforeAnyLookup()
    {
        var lookup = new FakeLookupClient(Found);
        var client = CreateClient(factory, enabled: true, lookup);

        var response = await client.PostAsync("/perf/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = @"DEFRA\a" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(lookup.ReceivedUsername);
        Assert.False(HasSessionCookie(response));
    }

    [Fact]
    public async Task WhenDisabled_AccountLoginStillChallengesEntra()
    {
        var client = CreateClient(factory, enabled: false);

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(PtlInternalWebTestFactory.FakeAuthorizationEndpoint, response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task WhenEnabled_AccountLoginRedirectsToTheLoadTestingPageKeepingReturnUrl()
    {
        var client = CreateClient(factory, enabled: true);

        var response = await client.GetAsync("/Account/Login?returnUrl=/dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/perf/login?returnUrl=%2Fdashboard", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task WhenEnabled_LoginPageRendersAFormPostingToItselfWithAnAntiforgeryToken()
    {
        var client = CreateClient(factory, enabled: true);

        var response = await client.GetAsync("/perf/login?returnUrl=/Contract");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Load testing sign in", body);
        Assert.Contains("action=\"/perf/login\"", body);
        Assert.Matches(AntiforgeryTokenPattern(), body);
        Assert.Contains("value=\"/Contract\"", body);
    }

    [Fact]
    public async Task WhenEnabled_LinkedUserSignsInWithTheStoredObjectIdAndRedirectsHome()
    {
        var lookup = new FakeLookupClient(Found);
        var api = new FakeInternalUserApiClient(Resolved);
        var client = CreateClient(factory, enabled: true, lookup, api);

        var response = await PostLoginAsync(client, @"  DEFRA\Tester ");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Home/Index", response.Headers.Location!.OriginalString);
        Assert.True(HasSessionCookie(response));
        Assert.Equal(@"DEFRA\Tester", lookup.ReceivedUsername);
        Assert.Equal(StoredObjectId, api.ReceivedRequest!.SsoIdInt);
        Assert.Equal(@"DEFRA\Tester", api.ReceivedRequest.Username);
    }

    [Fact]
    public async Task WhenEnabled_LocalReturnUrlIsHonouredAndExternalOneIgnored()
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(Found), new FakeInternalUserApiClient(Resolved));

        var local = await PostLoginAsync(client, @"DEFRA\a", "/Contract");
        var external = await PostLoginAsync(client, @"DEFRA\a", "https://evil.test/");

        Assert.Equal("/Contract", local.Headers.Location!.OriginalString);
        Assert.Equal("/Home/Index", external.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData(LoadTestingInternalLookupStatus.NotFound, "No PTL internal user")]
    [InlineData(LoadTestingInternalLookupStatus.NotLinked, "no stored object id")]
    [InlineData(LoadTestingInternalLookupStatus.Found, "No PTL internal user")]
    public async Task WhenEnabled_UserTheLookupDoesNotFindIsForbiddenWithItsReasonAndNoCookie(LoadTestingInternalLookupStatus status, string expectedText)
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(new LoadTestingInternalLookupResponse(status, null)));

        var response = await PostLoginAsync(client, @"DEFRA\nobody");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(expectedText, await response.Content.ReadAsStringAsync());
        Assert.False(HasSessionCookie(response));
    }

    [Fact]
    public async Task WhenEnabled_UserTheResolverDeniesIsForbiddenWithNoCookie()
    {
        var denied = new ResolveInternalUserResponse(false, null, string.Empty, string.Empty, []);
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(Found), new FakeInternalUserApiClient(denied));

        var response = await PostLoginAsync(client, @"DEFRA\inactive");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("This user cannot sign in (not resolved).", await response.Content.ReadAsStringAsync());
        Assert.False(HasSessionCookie(response));
    }

    [Fact]
    public async Task WhenEnabled_ApiWithTheLookupSwitchedOffIsReportedAsUnavailable()
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(null));

        var response = await PostLoginAsync(client, @"DEFRA\a");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("PTL.Api has no load-testing lookup", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("nodomain")]
    [InlineData(@"\name")]
    [InlineData(@"DEFRA\")]
    [InlineData(null)]
    public async Task WhenEnabled_BlankMissingOrMalformedUsernameIsABadRequest(string? username)
    {
        var lookup = new FakeLookupClient(Found);
        var client = CreateClient(factory, enabled: true, lookup);

        var response = await PostLoginAsync(client, username);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Enter the username as DOMAIN", await response.Content.ReadAsStringAsync());
        Assert.Null(lookup.ReceivedUsername);
    }
}
