using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Integration;

// Direct-URL protection through the real cookie pipeline (PtlInternalWebTestFactory opts out of the shared test user):
// the PTL roles ride the cookie as the ResolvedRoles claim, become role claims, and the policies decide.
public class AuthorizationRouteTests(PtlInternalWebTestFactory factory) : IClassFixture<PtlInternalWebTestFactory>
{
    private HttpClient SignedInClient(string? roles)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var url = roles is null ? PtlInternalWebTestFactory.TestSignInPath : $"{PtlInternalWebTestFactory.TestSignInPath}?roles={Uri.EscapeDataString(roles)}";
        var response = client.GetAsync(url).GetAwaiter().GetResult();
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return client;
    }

    [Theory]
    [InlineData("/Customer/Index")]
    [InlineData("/Contract/ManageContracts")]
    [InlineData("/Scheme/ManageSchemes")]
    [InlineData("/Distribution/Index")]
    [InlineData("/Distribution/Search")]
    [InlineData("/SystemAdministration/Index")]
    [InlineData("/Invoice/Index")]
    [InlineData("/Home/Index")]
    public async Task AnonymousRequest_IsSentToSignIn_NotToThePage(string url)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    private static string RedirectPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location!;
        return location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
    }

    [Theory]
    [InlineData("/Customer/Index")]
    [InlineData("/Contract/ManageContracts")]
    [InlineData("/Scheme/ManageSchemes")]
    [InlineData("/Distribution/Index")]
    [InlineData("/Distribution/Search")]
    [InlineData("/SystemAdministration/Index")]
    [InlineData("/Invoice/Index")]
    public async Task SignedInUserWithoutThePtlRole_IsRedirectedToAccessDenied(string url)
    {
        var client = SignedInClient(roles: null);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/AccessDenied", RedirectPath(response));
    }

    [Theory]
    [InlineData("Contracts Admin", "/SystemAdministration/Index")]
    [InlineData("Admin", "/Contract/ManageContracts")]
    [InlineData("Contracts Admin", "/Scheme/ManageSchemes")]
    [InlineData("Scheme Admin", "/Distribution/Index")]
    [InlineData("Contracts Admin", "/Invoice/Index")]
    [InlineData("Generate Invoices", "/Invoice/Index")]
    [InlineData("Admin", "/Test%20Consultant")]
    [InlineData("Internal User,Distributions", "/Distribution/Schedule?year=2026&monthId=4")]
    [InlineData("Scheduling,Distributions", "/Distribution/Preparation?year=2026&monthId=4")]
    [InlineData("Scheme Admin", "/Distribution/Search")]
    public async Task SignedInUserWithADifferentRole_IsRefused_AdminIsNotASuperuser(string roles, string url)
    {
        var client = SignedInClient(roles);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/AccessDenied", RedirectPath(response));
    }

    [Theory]
    [InlineData("Admin", "/SystemAdministration/Index")]
    [InlineData("Contracts Admin", "/Contract/ManageContracts")]
    [InlineData("Scheme Admin", "/Scheme/ManageSchemes")]
    public async Task SignedInUserWithThePtlRole_ReachesThePage(string roles, string url)
    {
        var client = SignedInClient(roles);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Regression: real model binding validates both tabs' forms on every request, so the Sample
    // Number tab's required-field error must not appear before Search has ever been clicked. The
    // message text itself is still present in the input's unobtrusive-validation data attribute
    // (harmless, inert without a submit), so this checks the actual rendered error markup instead.
    [Fact]
    public async Task DistributionSearch_FirstVisit_DoesNotShowTheSampleNumberValidationMessage()
    {
        var client = SignedInClient("Distributions");

        var response = await client.GetAsync("/Distribution/Search");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("govuk-error-summary__list", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SampleNumber_SampleNumber-error", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccessDeniedPage_AnswersForbiddenWithTheStandardMessage()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/AccessDenied");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Access Denied</h1>", body, StringComparison.Ordinal);
        Assert.Contains("Shown when access to the requested page is restricted.", body, StringComparison.Ordinal);
        Assert.Contains("To gain access, contact a System Administrator.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomePage_ShowsOnlyTheMenuEntriesTheUsersRolesOpen()
    {
        var client = SignedInClient("Internal User,Distributions");

        var response = await client.GetAsync("/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(">Distributions<", body, StringComparison.Ordinal);
        Assert.DoesNotContain(">System Administration<", body, StringComparison.Ordinal);
        Assert.DoesNotContain(">Manage Contracts<", body, StringComparison.Ordinal);
        Assert.DoesNotContain(">Manage Schemes<", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomePage_ForAnAdmin_ShowsSystemAdministrationAndNothingElse()
    {
        var client = SignedInClient("Admin");

        var response = await client.GetAsync("/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains(">System Administration<", body, StringComparison.Ordinal);
        Assert.DoesNotContain(">Distributions<", body, StringComparison.Ordinal);
        Assert.DoesNotContain(">Manage Contracts<", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccountSignedOutAndNotPermitted_StayReachableWithoutSigningIn()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/SignedOut")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/NotPermitted")).StatusCode);
    }
}
