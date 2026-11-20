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
using PTL.Contracts.ExternalUser;
using PTL.Contracts.LoadTesting;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.LoadTesting;

public partial class LoadTestingLoginTests(PtlExternalWebTestFactory factory) : IClassFixture<PtlExternalWebTestFactory>
{
    private static readonly Guid StoredContactId = Guid.Parse("321cbd77-fec2-f111-aaad-7c1e52845f5a");

    private static readonly ResolveExternalUserResponse Resolved = new(
        ExternalUserResolutionStatus.Resolved,
        "Load Tester",
        ["Participant", "Viewer"],
        [],
        ExternalUserType.Participant);

    [GeneratedRegex("__RequestVerificationToken[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenPattern();

    private sealed class FakeLookupClient(LoadTestingLookupResponse? response) : ILoadTestingLookupClient
    {
        public string? ReceivedEmail { get; private set; }

        public Task<LoadTestingLookupResponse?> LookupExternalUserAsync(string email, CancellationToken cancellationToken)
        {
            ReceivedEmail = email;
            return Task.FromResult(response);
        }

        public Task<LoadTestingInternalLookupResponse?> LookupInternalUserAsync(string username, CancellationToken cancellationToken) =>
            throw new NotSupportedException("PTL.ExternalWeb only looks up external users.");
    }

    private static LoadTestingLookupResponse Found => new(LoadTestingLookupStatus.Found, StoredContactId);

    private static WebApplicationFactory<Program> CreateApp(
        WebApplicationFactory<Program> source,
        bool enabled,
        ILoadTestingLookupClient? lookup = null,
        FakeExternalUserApiClient? api = null,
        string? expiresOn = null) =>
        source.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LoadTesting:Enabled"] = enabled ? "true" : "false",
                ["LoadTesting:ExpiresOn"] = expiresOn ?? DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            }));

            builder.ConfigureServices(services =>
            {
                if (lookup is not null)
                {
                    services.AddSingleton(lookup);
                }

                if (api is not null)
                {
                    services.AddSingleton<IExternalUserApiClient>(api);
                }
            });
        });

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> source,
        bool enabled,
        ILoadTestingLookupClient? lookup = null,
        FakeExternalUserApiClient? api = null) =>
        CreateApp(source, enabled, lookup, api).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // Posts the form the way a browser (or a JMeter script) does: token and cookie from the rendered page.
    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string? email, string? returnUrl = null)
    {
        var page = await client.GetAsync("/perf/login");
        var token = AntiforgeryTokenPattern().Match(await page.Content.ReadAsStringAsync()).Groups[1].Value;
        var cookie = string.Join("; ", page.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));

        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["returnUrl"] = returnUrl ?? string.Empty };
        if (email is not null)
        {
            fields["email"] = email;
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
    public async Task WhenEnabledButExpired_LoginPageIsNotFoundAndAccountLoginStillChallengesCidm()
    {
        var client = CreateApp(factory, enabled: true, expiresOn: "2000-01-01")
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/perf/login")).StatusCode);
        var login = await client.GetAsync("/Account/Login");
        Assert.DoesNotContain("/perf/login", login.Headers.Location?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenDisabled_LoginPageAndPostAreNotFound()
    {
        var app = CreateApp(factory, enabled: false);
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (cookie, token) = IssueAntiforgeryTokens(app.Services);
        using var post = new HttpRequestMessage(HttpMethod.Post, "/perf/login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["email"] = "a@b.test" })
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

        var response = await client.PostAsync("/perf/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = "a@example.test" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(lookup.ReceivedEmail);
        Assert.False(HasSessionCookie(response));
    }

    [Fact]
    public async Task WhenDisabled_AccountLoginStillChallengesCidm()
    {
        var client = CreateClient(factory, enabled: false);

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(PtlExternalWebTestFactory.FakeAuthorizationEndpoint, response.Headers.Location!.ToString());
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

        var response = await client.GetAsync("/perf/login?returnUrl=/Order");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Load testing sign in", body);
        Assert.Contains("action=\"/perf/login\"", body);
        Assert.Matches(AntiforgeryTokenPattern(), body);
        Assert.Contains("value=\"/Order\"", body);
    }

    [Fact]
    public async Task WhenEnabled_LinkedUserSignsInWithTheStoredContactIdAndRedirectsHome()
    {
        var lookup = new FakeLookupClient(Found);
        var api = new FakeExternalUserApiClient(Resolved);
        var client = CreateClient(factory, enabled: true, lookup, api);

        var response = await PostLoginAsync(client, "  Tester@Example.test ");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Home/Index", response.Headers.Location!.OriginalString);
        Assert.True(HasSessionCookie(response));
        Assert.Equal("tester@example.test", lookup.ReceivedEmail);
        Assert.Equal(StoredContactId, api.ReceivedRequest!.SsoIdExt);
        Assert.Equal("tester@example.test", api.ReceivedRequest.Email);
    }

    [Fact]
    public async Task WhenEnabled_LocalReturnUrlIsHonouredAndExternalOneIgnored()
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(Found), new FakeExternalUserApiClient(Resolved));

        var local = await PostLoginAsync(client, "a@example.test", "/Order");
        var external = await PostLoginAsync(client, "a@example.test", "https://evil.test/");

        Assert.Equal("/Order", local.Headers.Location!.OriginalString);
        Assert.Equal("/Home/Index", external.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData(LoadTestingLookupStatus.NotFound, "No PTL external user")]
    [InlineData(LoadTestingLookupStatus.NotLinked, "no stored contact id")]
    [InlineData(LoadTestingLookupStatus.Ambiguous, "more than one contact id")]
    [InlineData(LoadTestingLookupStatus.Found, "No PTL external user")]
    public async Task WhenEnabled_UserTheLookupDoesNotFindIsForbiddenWithItsReasonAndNoCookie(LoadTestingLookupStatus status, string expectedText)
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(new LoadTestingLookupResponse(status, null)));

        var response = await PostLoginAsync(client, "nobody@example.test");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(expectedText, await response.Content.ReadAsStringAsync());
        Assert.False(HasSessionCookie(response));
    }

    [Fact]
    public async Task WhenEnabled_UserTheResolverDeniesIsForbiddenWithNoCookie()
    {
        var denied = ResolveExternalUserResponse.Denied(ExternalUserResolutionStatus.Inactive, string.Empty);
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(Found), new FakeExternalUserApiClient(denied));

        var response = await PostLoginAsync(client, "inactive@example.test");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("inactive, no active lab, or not resolved", await response.Content.ReadAsStringAsync());
        Assert.False(HasSessionCookie(response));
    }

    [Fact]
    public async Task WhenEnabled_ApiWithTheLookupSwitchedOffIsReportedAsUnavailable()
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(null));

        var response = await PostLoginAsync(client, "a@example.test");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("PTL.Api has no load-testing lookup", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task WhenEnabled_BlankOrMissingEmailIsABadRequest(string? email)
    {
        var lookup = new FakeLookupClient(Found);
        var client = CreateClient(factory, enabled: true, lookup);

        var response = await PostLoginAsync(client, email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Enter an email address.", await response.Content.ReadAsStringAsync());
        Assert.Null(lookup.ReceivedEmail);
    }
}
