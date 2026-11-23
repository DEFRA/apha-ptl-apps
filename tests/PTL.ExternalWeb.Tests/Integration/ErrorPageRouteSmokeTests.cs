using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PTL.ApiClient;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// The friendly General Error, Page Not Found and Access Denied pages through the full pipeline (status code
// re-execution, layout, SupportContact view component) - parity with PTL.InternalWeb's error pages.
public class ErrorPageRouteSmokeTests : IClassFixture<PtlExternalWebTestFactory>
{
    private const string SupportMailto = $"href=\"mailto:{FakeExternalSiteMessageApiClient.DefaultSupportEmailAddress}\"";

    private readonly PtlExternalWebTestFactory _factory;

    public ErrorPageRouteSmokeTests(PtlExternalWebTestFactory factory) => _factory = factory;

    [Fact]
    public async Task PageNotFound_Anonymous_ShowsTheFriendlyPage_WithTheSupportEmail_AndNoBreadcrumbs()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Error/404");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Page not found</h1>", body);
        Assert.Contains("If the web address is correct or you selected a link or button, contact the Animal and Plant Health Agency at", body);
        Assert.Contains(SupportMailto, body);
        Assert.DoesNotContain("govuk-breadcrumbs", body);
    }

    // Every page needs a signed-in user (fallback policy), and with no endpoint matched that policy still applies -
    // so an anonymous unknown URL is sent to CIDM sign-in rather than told whether the page exists.
    [Fact]
    public async Task UnknownUrl_Anonymous_IsSentToSignIn()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/no-such-page/really");

        AssertSentToSignIn(response, "/no-such-page/really");
    }

    [Theory]
    [InlineData("/no-such-page/really")]
    [InlineData("/Home/NoSuchAction")]
    public async Task UnknownUrl_SignedIn_ShowsTheFriendlyPageNotFound(string path)
    {
        var client = _factory.CreateClient();
        var cookies = await SignInAsync(client, "Participant");

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, path, cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Page not found</h1>", body);
        Assert.Contains(SupportMailto, body);
        Assert.DoesNotContain("govuk-breadcrumbs", body);
    }

    [Theory]
    [InlineData("/Error/500", HttpStatusCode.InternalServerError)]
    [InlineData("/Error/400", HttpStatusCode.BadRequest)]
    [InlineData("/Error/503", HttpStatusCode.ServiceUnavailable)]
    public async Task GeneralError_ShowsTheFriendlyPage_WithoutTechnicalDetails(string path, HttpStatusCode expectedStatus)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Sorry, there is a problem with the service</h1>", body);
        Assert.Contains("Try again later.", body);
        Assert.Contains("If the problem continues, contact the Animal and Plant Health Agency at", body);
        Assert.Contains(SupportMailto, body);
        Assert.Contains("If you contact us, quote this reference:", body);
        AssertNoTechnicalDetails(body);
        Assert.DoesNotContain("govuk-breadcrumbs", body);
    }

    [Fact]
    public async Task GeneralError_IsNotCached()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Error/500");

        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Forbidden_ShowsTheAccessDeniedPage_AndKeeps403()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Error/403");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-l\">You do not have permission to view this page</h1>", body);
        Assert.Contains("To get access, contact the Animal and Plant Health Agency at", body);
        Assert.Contains(SupportMailto, body);
        Assert.DoesNotContain("govuk-breadcrumbs", body);
    }

    [Fact]
    public async Task AccessDenied_ShowsTheSupportEmail_AndAnswers403()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/AccessDenied");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("<title>Access denied", body);
        Assert.Contains("You do not have permission to view this page", body);
        Assert.Contains(SupportMailto, body);
        Assert.DoesNotContain("govuk-breadcrumbs", body);
    }

    [Fact]
    public async Task OutOfRangeStatus_IsNotAnErrorPageRoute()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Error/200");

        // No /Error/200 endpoint: the signed-out request is challenged, as any other unknown URL is.
        AssertSentToSignIn(response, "/Error/200");
    }

    [Theory]
    [InlineData("/Error/404", HttpStatusCode.NotFound, "If the web address is correct or you selected a link or button, contact the Animal and Plant Health Agency.</p>")]
    [InlineData("/Error/500", HttpStatusCode.InternalServerError, "If the problem continues, contact the Animal and Plant Health Agency.</p>")]
    [InlineData("/AccessDenied", HttpStatusCode.Forbidden, "To get access, contact the Animal and Plant Health Agency.</p>")]
    public async Task SupportEmailUnavailable_StillShowsTheErrorPage_WithoutAnEmail(string path, HttpStatusCode expectedStatus, string expectedSentence)
    {
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExternalSiteMessageApiClient>();
                services.AddSingleton<IExternalSiteMessageApiClient>(
                    new FakeExternalSiteMessageApiClient("support@apha.test", new HttpRequestException("PTL.Api is unreachable")));
            })).CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Contains(expectedSentence, body);
        Assert.DoesNotContain("mailto:", body);
        AssertNoTechnicalDetails(body);
    }

    [Fact]
    public async Task SupportEmailNotSet_ShowsTheErrorPage_WithoutAnEmail()
    {
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExternalSiteMessageApiClient>();
                services.AddSingleton<IExternalSiteMessageApiClient>(new FakeExternalSiteMessageApiClient(string.Empty));
            })).CreateClient();

        var response = await client.GetAsync("/Error/404");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("contact the Animal and Plant Health Agency.</p>", body);
        Assert.DoesNotContain("mailto:", body);
    }

    // Outside Development an unhandled exception goes to UseExceptionHandler("/Home/Error"); the default test host is
    // Development (developer exception page), so this factory runs as Production.
    [Fact]
    public async Task UnhandledException_InProduction_ShowsTheGeneralErrorPage_WithoutTechnicalDetails()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISystemMessageApiClient>();
                services.AddScoped<ISystemMessageApiClient>(_ => new ThrowingSystemMessageApiClient());
            });
        }).CreateClient();
        var cookies = await SignInAsync(client, "Participant");

        var response = await CookieForwardingHttpClient.SendWithCookiesAsync(client, HttpMethod.Get, "/Home/Index", cookies);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Sorry, there is a problem with the service</h1>", body);
        Assert.Contains(SupportMailto, body);
        Assert.Contains("If you contact us, quote this reference:", body);
        Assert.DoesNotContain(ThrowingSystemMessageApiClient.SecretDetail, body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
        AssertNoTechnicalDetails(body);
        Assert.DoesNotContain("govuk-breadcrumbs", body);
    }

    // The cookie scheme's LoginPath, which then challenges CIDM (see AccountRouteSmokeTests.Login_Get_RedirectsToCidmAuthorizeEndpoint).
    private static void AssertSentToSignIn(HttpResponseMessage response, string requestedPath)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("/Account/Login", location.AbsolutePath);
        Assert.Equal($"?ReturnUrl={Uri.EscapeDataString(requestedPath)}", location.Query);
    }

    private static void AssertNoTechnicalDetails(string body)
    {
        Assert.DoesNotContain("Development Mode", body);
        Assert.DoesNotContain("ASPNETCORE_ENVIRONMENT", body);
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("StackTrace", body);
        Assert.DoesNotContain("Stack trace", body, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, string>> SignInAsync(HttpClient client, string roles)
    {
        var cookies = new Dictionary<string, string>();
        var signInResponse = await client.GetAsync($"{PtlExternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString(roles)}");
        CookieForwardingHttpClient.CaptureCookies(signInResponse, cookies);
        return cookies;
    }

    private sealed class ThrowingSystemMessageApiClient : ISystemMessageApiClient
    {
        public const string SecretDetail = "connection string Server=secret-host";

        public Task<PTL.Contracts.SystemMessage.GetImportantMessageResponse> GetImportantMessageAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(SecretDetail);

        public Task<PTL.Contracts.SystemMessage.GetMessageResponse> GetMessageAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(SecretDetail);
    }
}
