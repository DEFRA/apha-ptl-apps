using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using PTL.Common.Auth;

namespace PTL.Api.Tests.Common;

public class OpenIdConnectEventHelpersTests
{
    [Fact]
    public async Task ResolveOrDenyAsync_WithNullResolver_LeavesIdentityUnchanged()
    {
        var context = CreateTokenValidatedContext();
        var identity = new ClaimsIdentity();

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync(context, identity, resolve: null);

        Assert.Empty(identity.Claims);
        Assert.False(context.Result?.Handled ?? false);
    }

    [Fact]
    public async Task ResolveOrDenyAsync_WhenDenied_HandlesResponseAndRedirectsToDenialPath()
    {
        var context = CreateTokenValidatedContext();
        var identity = new ClaimsIdentity();
        var resolution = new FakeIdentityResolution(isAllowed: false, denialRedirectPath: "/denied");

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync(context, identity, (_, _) => Task.FromResult<IIdentityResolution>(resolution));

        Assert.True(context.Result?.Handled);
        Assert.Equal("/denied", context.HttpContext.Response.Headers.Location.ToString());
        Assert.Empty(identity.Claims);
    }

    [Fact]
    public async Task ResolveOrDenyAsync_WhenDeniedWithoutRedirectPath_RedirectsToRoot()
    {
        var context = CreateTokenValidatedContext();
        var identity = new ClaimsIdentity();
        var resolution = new FakeIdentityResolution(isAllowed: false, denialRedirectPath: null);

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync(context, identity, (_, _) => Task.FromResult<IIdentityResolution>(resolution));

        Assert.Equal("/", context.HttpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task ResolveOrDenyAsync_WhenAllowedWithClaims_AddsThemToIdentity()
    {
        var context = CreateTokenValidatedContext();
        var identity = new ClaimsIdentity();
        var claims = new Dictionary<string, string> { ["role"] = "Viewer" };
        var resolution = new FakeIdentityResolution(isAllowed: true, claims: claims);

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync(context, identity, (_, _) => Task.FromResult<IIdentityResolution>(resolution));

        Assert.Single(identity.Claims, c => c.Type == "role" && c.Value == "Viewer");
        Assert.False(context.Result?.Handled ?? false);
    }

    [Fact]
    public async Task ResolveOrDenyAsync_WhenAllowedWithoutClaims_AddsNoClaims()
    {
        var context = CreateTokenValidatedContext();
        var identity = new ClaimsIdentity();
        var resolution = new FakeIdentityResolution(isAllowed: true, claims: null);

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync(context, identity, (_, _) => Task.FromResult<IIdentityResolution>(resolution));

        Assert.Empty(identity.Claims);
    }

    [Fact]
    public async Task HandleRemoteFailureAsync_LogsFailureAndRedirectsToErrorPage()
    {
        var httpContext = new DefaultHttpContext();
        var scheme = new AuthenticationScheme("oidc", null, typeof(OpenIdConnectHandler));
        var options = new OpenIdConnectOptions();
        var failure = new InvalidOperationException("boom");
        var context = new RemoteFailureContext(httpContext, scheme, options, failure);

        Exception? logged = null;
        await OpenIdConnectEventHelpers.HandleRemoteFailureAsync(context, ex => logged = ex);

        Assert.Same(failure, logged);
        Assert.True(context.Result?.Handled);
        Assert.Equal("/Home/Error", httpContext.Response.Headers.Location.ToString());
    }

    private static TokenValidatedContext CreateTokenValidatedContext()
    {
        var httpContext = new DefaultHttpContext();
        var scheme = new AuthenticationScheme("oidc", null, typeof(OpenIdConnectHandler));
        var options = new OpenIdConnectOptions();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var properties = new AuthenticationProperties();

        return new TokenValidatedContext(httpContext, scheme, options, principal, properties);
    }

    private sealed class FakeIdentityResolution(bool isAllowed, string? denialRedirectPath = null, IReadOnlyDictionary<string, string>? claims = null) : IIdentityResolution
    {
        public bool IsAllowed => isAllowed;
        public string? DenialRedirectPath => denialRedirectPath;
        public IReadOnlyDictionary<string, string>? Claims => claims;
    }
}
