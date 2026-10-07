using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using PTL.Common.Auth;

namespace PTL.Common.Tests.Auth;

public class OpenIdConnectEventHelpersTests
{
    private sealed record FakeIdentityResolution(bool IsAllowed, string? DenialRedirectPath = null, IReadOnlyDictionary<string, string>? Claims = null)
        : IIdentityResolution;

    private static AuthenticationScheme CreateScheme() => new("oidc", "oidc", typeof(OpenIdConnectHandler));

    private static TokenValidatedContext CreateContext(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext();
        return new TokenValidatedContext(httpContext, CreateScheme(), new OpenIdConnectOptions(), principal, new AuthenticationProperties());
    }

    [Fact]
    public async Task ResolveOrDenyAsync_ResolveIsNull_LeavesPrincipalUnchanged()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var context = CreateContext(principal);

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync(context, identity, resolve: null);

        Assert.Single(principal.Claims);
        Assert.False(context.Result?.Handled ?? false);
    }

    [Fact]
    public async Task ResolveOrDenyAsync_ResolutionAllows_AddsReturnedClaims()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var context = CreateContext(principal);
        var resolution = new FakeIdentityResolution(true, Claims: new Dictionary<string, string> { ["role"] = "Admin" });

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync(context, identity, (_, _) => Task.FromResult<IIdentityResolution>(resolution));

        Assert.Contains(identity.Claims, c => c.Type == "role" && c.Value == "Admin");
        Assert.False(context.Result?.Handled ?? false);
    }

    [Fact]
    public async Task ResolveOrDenyAsync_ResolutionDenies_HandlesResponseAndRedirectsToDenialPath()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var context = CreateContext(principal);
        var resolution = new FakeIdentityResolution(false, DenialRedirectPath: "/Account/NotPermitted");

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync(context, identity, (_, _) => Task.FromResult<IIdentityResolution>(resolution));

        Assert.True(context.Result?.Handled);
        Assert.Equal(StatusCodes.Status302Found, context.HttpContext.Response.StatusCode);
        Assert.Equal("/Account/NotPermitted", context.HttpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task ResolveOrDenyAsync_ResolutionDeniesWithNoRedirectPath_RedirectsToRoot()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var context = CreateContext(principal);
        var resolution = new FakeIdentityResolution(false);

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync(context, identity, (_, _) => Task.FromResult<IIdentityResolution>(resolution));

        Assert.Equal("/", context.HttpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task HandleRemoteFailureAsync_LogsAndRedirectsToGenericErrorPage()
    {
        var httpContext = new DefaultHttpContext();
        var context = new RemoteFailureContext(httpContext, CreateScheme(), new OpenIdConnectOptions(), new InvalidOperationException("boom"));
        Exception? loggedException = null;

        await OpenIdConnectEventHelpers.HandleRemoteFailureAsync(context, ex => loggedException = ex);

        Assert.True(context.Result?.Handled);
        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/Home/Error", httpContext.Response.Headers.Location.ToString());
        Assert.IsType<InvalidOperationException>(loggedException);
    }
}
