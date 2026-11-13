using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PTL.ApiClient;
using PTL.Contracts.InternalUser;
using PTL.Contracts.LoadTesting;
using PTL.InternalWeb.Features.LoadTesting;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Features.LoadTesting;

public class LoadTestingLoginTests(PtlInternalWebTestFactory factory) : IClassFixture<PtlInternalWebTestFactory>
{
    private static readonly Guid StoredObjectId = Guid.Parse("321cbd77-fec2-f111-aaad-7c1e52845f5a");

    private static readonly ResolveInternalUserResponse Resolved = new(true, Guid.NewGuid(), "Load Tester", "Labs", ["Admin"]);

    private sealed class FakeLookupClient(LoadTestingInternalLookupResponse? response) : ILoadTestingLookupClient
    {
        public string? ReceivedUsername { get; private set; }

        public Task<LoadTestingInternalLookupResponse?> LookupAsync(string username, CancellationToken cancellationToken)
        {
            ReceivedUsername = username;
            return Task.FromResult(response);
        }
    }

    private static LoadTestingInternalLookupResponse Found => new(LoadTestingInternalLookupStatus.Found, StoredObjectId);

    private static HttpClient CreateClient(
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
        }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static FormUrlEncodedContent Form(string username, string? returnUrl = null) =>
        new(new Dictionary<string, string> { ["username"] = username, ["returnUrl"] = returnUrl ?? string.Empty });

    private static bool HasSessionCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
        && values.Any(cookie => cookie.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));

    [Fact]
    public async Task WhenDisabled_LoginPageAndPostAreNotFound()
    {
        var client = CreateClient(factory, enabled: false);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/perf/login")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/perf/login", Form(@"DEFRA\a"))).StatusCode);
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
    public async Task WhenEnabled_LoginPageRenders()
    {
        var client = CreateClient(factory, enabled: true);

        var response = await client.GetAsync("/perf/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Load testing sign in", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WhenEnabled_LinkedUserSignsInWithTheStoredObjectIdAndRedirectsHome()
    {
        var lookup = new FakeLookupClient(Found);
        var api = new FakeInternalUserApiClient(Resolved);
        var client = CreateClient(factory, enabled: true, lookup, api);

        var response = await client.PostAsync("/perf/login", Form(@"  DEFRA\Tester "));

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

        var local = await client.PostAsync("/perf/login", Form(@"DEFRA\a", "/Contract"));
        var external = await client.PostAsync("/perf/login", Form(@"DEFRA\a", "https://evil.test/"));

        Assert.Equal("/Contract", local.Headers.Location!.OriginalString);
        Assert.Equal("/Home/Index", external.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData(LoadTestingInternalLookupStatus.NotFound, "No PTL internal user")]
    [InlineData(LoadTestingInternalLookupStatus.NotLinked, "no stored object id")]
    public async Task WhenEnabled_UserTheLookupDoesNotFindIsForbiddenWithItsReasonAndNoCookie(LoadTestingInternalLookupStatus status, string expectedText)
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(new LoadTestingInternalLookupResponse(status, null)));

        var response = await client.PostAsync("/perf/login", Form(@"DEFRA\nobody"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(expectedText, await response.Content.ReadAsStringAsync());
        Assert.False(HasSessionCookie(response));
    }

    [Fact]
    public async Task WhenEnabled_UserTheResolverDeniesIsForbiddenWithNoCookie()
    {
        var denied = new ResolveInternalUserResponse(false, null, string.Empty, string.Empty, []);
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(Found), new FakeInternalUserApiClient(denied));

        var response = await client.PostAsync("/perf/login", Form(@"DEFRA\inactive"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(HasSessionCookie(response));
    }

    [Fact]
    public async Task WhenEnabled_ApiWithTheLookupSwitchedOffIsReportedAsUnavailable()
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(null));

        var response = await client.PostAsync("/perf/login", Form(@"DEFRA\a"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("nodomain")]
    [InlineData(@"\name")]
    [InlineData(@"DEFRA\")]
    public async Task WhenEnabled_BlankOrMalformedUsernameIsABadRequest(string username)
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(Found));

        var response = await client.PostAsync("/perf/login", Form(username));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
