using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PTL.ApiClient;
using PTL.Contracts.ExternalUser;
using PTL.Contracts.LoadTesting;
using PTL.ExternalWeb.Features.LoadTesting;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Features.LoadTesting;

public class LoadTestingLoginTests(PtlExternalWebTestFactory factory) : IClassFixture<PtlExternalWebTestFactory>
{
    private static readonly Guid StoredContactId = Guid.Parse("321cbd77-fec2-f111-aaad-7c1e52845f5a");

    private static readonly ResolveExternalUserResponse Resolved = new(
        ExternalUserResolutionStatus.Resolved,
        "Load Tester",
        ["Participant", "Viewer"],
        [],
        ExternalUserType.Participant);

    private sealed class FakeLookupClient(LoadTestingLookupResponse? response) : ILoadTestingLookupClient
    {
        public string? ReceivedEmail { get; private set; }

        public Task<LoadTestingLookupResponse?> LookupAsync(string email, CancellationToken cancellationToken)
        {
            ReceivedEmail = email;
            return Task.FromResult(response);
        }
    }

    private static LoadTestingLookupResponse Found => new(LoadTestingLookupStatus.Found, StoredContactId);

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> source,
        bool enabled,
        ILoadTestingLookupClient? lookup = null,
        FakeExternalUserApiClient? api = null) =>
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
                    services.AddSingleton<IExternalUserApiClient>(api);
                }
            });
        }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static FormUrlEncodedContent Form(string email, string? returnUrl = null) =>
        new(new Dictionary<string, string> { ["email"] = email, ["returnUrl"] = returnUrl ?? string.Empty });

    [Fact]
    public async Task WhenDisabled_LoginPageAndPostAreNotFound()
    {
        var client = CreateClient(factory, enabled: false);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/perf/login")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/perf/login", Form("a@b.test"))).StatusCode);
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
    public async Task WhenEnabled_LoginPageRenders()
    {
        var client = CreateClient(factory, enabled: true);

        var response = await client.GetAsync("/perf/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Load testing sign in", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WhenEnabled_LinkedUserSignsInWithTheStoredContactIdAndRedirectsHome()
    {
        var lookup = new FakeLookupClient(Found);
        var api = new FakeExternalUserApiClient(Resolved);
        var client = CreateClient(factory, enabled: true, lookup, api);

        var response = await client.PostAsync("/perf/login", Form("  Tester@Example.test "));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Home/Index", response.Headers.Location!.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));
        Assert.Equal("tester@example.test", lookup.ReceivedEmail);
        Assert.Equal(StoredContactId, api.ReceivedRequest!.SsoIdExt);
        Assert.Equal("tester@example.test", api.ReceivedRequest.Email);
    }

    [Fact]
    public async Task WhenEnabled_LocalReturnUrlIsHonouredAndExternalOneIgnored()
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(Found), new FakeExternalUserApiClient(Resolved));

        var local = await client.PostAsync("/perf/login", Form("a@example.test", "/Order"));
        var external = await client.PostAsync("/perf/login", Form("a@example.test", "https://evil.test/"));

        Assert.Equal("/Order", local.Headers.Location!.OriginalString);
        Assert.Equal("/Home/Index", external.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData(LoadTestingLookupStatus.NotFound, "No PTL external user")]
    [InlineData(LoadTestingLookupStatus.NotLinked, "no stored contact id")]
    [InlineData(LoadTestingLookupStatus.Ambiguous, "more than one contact id")]
    public async Task WhenEnabled_UserTheLookupDoesNotFindIsForbiddenWithItsReasonAndNoCookie(LoadTestingLookupStatus status, string expectedText)
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(new LoadTestingLookupResponse(status, null)));

        var response = await client.PostAsync("/perf/login", Form("nobody@example.test"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(expectedText, await response.Content.ReadAsStringAsync());
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        Assert.DoesNotContain(cookies, cookie => cookie.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenEnabled_UserTheResolverDeniesIsForbiddenWithNoCookie()
    {
        var denied = ResolveExternalUserResponse.Denied(ExternalUserResolutionStatus.Inactive, string.Empty);
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(Found), new FakeExternalUserApiClient(denied));

        var response = await client.PostAsync("/perf/login", Form("inactive@example.test"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        Assert.DoesNotContain(cookies, cookie => cookie.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenEnabled_ApiWithTheLookupSwitchedOffIsReportedAsUnavailable()
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(null));

        var response = await client.PostAsync("/perf/login", Form("a@example.test"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task WhenEnabled_BlankEmailIsABadRequest()
    {
        var client = CreateClient(factory, enabled: true, new FakeLookupClient(Found));

        var response = await client.PostAsync("/perf/login", Form("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
