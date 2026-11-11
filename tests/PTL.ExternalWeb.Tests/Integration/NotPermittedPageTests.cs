using System.Net;
using PTL.ExternalWeb.Features.Account;
using PTL.ExternalWeb.Tests.TestSupport;

namespace PTL.ExternalWeb.Tests.Integration;

// The sign-in denial page is reached with no local cookie (see AccountController.NotPermitted), so it is fetched anonymously.
public class NotPermittedPageTests(PtlExternalWebTestFactory factory) : IClassFixture<PtlExternalWebTestFactory>
{
    private async Task<string> GetBodyAsync(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task NoActiveLabReason_ExplainsInPlainWordsThatNoLabIsSwitchedOnForTheUser()
    {
        var body = await GetBodyAsync("/Account/NotPermitted?reason=no-active-lab");

        Assert.Contains("You are not linked to any lab at the moment", body);
        Assert.Contains("has not been switched on for you", body);
        Assert.DoesNotContain("not currently active", body);
        Assert.DoesNotContain("not set up to access", body);
    }

    [Fact]
    public async Task InactiveReason_ShowsTheInactiveAccountMessage()
    {
        var body = await GetBodyAsync("/Account/NotPermitted?reason=inactive");

        Assert.Contains("not currently active", body);
        Assert.DoesNotContain("not linked to any lab", body);
    }

    [Theory]
    [InlineData("/Account/NotPermitted")]
    [InlineData("/Account/NotPermitted?reason=something-unknown")]
    public async Task NoReasonOrAnUnknownOne_ShowsTheGenericMessageWithoutEchoingTheReason(string path)
    {
        var body = await GetBodyAsync(path);

        Assert.Contains("not set up to access the Proficiency Testing external service", body);
        Assert.DoesNotContain("something-unknown", body);
    }

    [Fact]
    public void PathFor_BuildsTheDenialPageUrlWithTheOptionalReason()
    {
        Assert.Equal("/Account/NotPermitted", NotPermittedReasons.PathFor());
        Assert.Equal("/Account/NotPermitted?reason=inactive", NotPermittedReasons.PathFor(NotPermittedReasons.Inactive));
        Assert.Equal("/Account/NotPermitted?reason=no-active-lab", NotPermittedReasons.PathFor(NotPermittedReasons.NoActiveLab));
    }
}
