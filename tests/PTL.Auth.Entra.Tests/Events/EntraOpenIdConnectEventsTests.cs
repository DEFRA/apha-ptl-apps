using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PTL.Auth.Entra.Events;

namespace PTL.Auth.Entra.Tests.Events;

public class EntraOpenIdConnectEventsTests
{
    private sealed class FakeEntraInternalUserResolver(EntraInternalUserResolution resolution) : IEntraInternalUserResolver
    {
        public ClaimsPrincipal? ReceivedPrincipal { get; private set; }

        public Task<EntraInternalUserResolution> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
        {
            ReceivedPrincipal = principal;
            return Task.FromResult(resolution);
        }
    }

    private static EntraOpenIdConnectEvents CreateEvents() => new(NullLogger<EntraOpenIdConnectEvents>.Instance);

    private static AuthenticationScheme CreateScheme() => new("oidc", "oidc", typeof(OpenIdConnectHandler));

    private static TokenValidatedContext CreateTokenValidatedContext(ClaimsPrincipal principal, IEntraInternalUserResolver? resolver = null)
    {
        var services = new ServiceCollection();
        if (resolver is not null)
        {
            services.AddSingleton(resolver);
        }

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        return new TokenValidatedContext(httpContext, CreateScheme(), new OpenIdConnectOptions(), principal, new AuthenticationProperties());
    }

    [Fact]
    public async Task TokenValidated_NoResolverRegistered_LeavesPrincipalUnchanged()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var context = CreateTokenValidatedContext(principal);

        await CreateEvents().TokenValidated(context);

        Assert.Single(principal.Claims);
        Assert.False(context.Result?.Handled ?? false);
    }

    [Fact]
    public async Task TokenValidated_NoRequestServices_LeavesPrincipalUnchanged()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var context = new TokenValidatedContext(new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), principal, new AuthenticationProperties());

        var exception = await Record.ExceptionAsync(() => CreateEvents().TokenValidated(context));

        Assert.Null(exception);
        Assert.Single(principal.Claims);
    }

    [Fact]
    public async Task TokenValidated_PrincipalWithoutClaimsIdentity_ReturnsWithoutThrowing()
    {
        var principal = new ClaimsPrincipal();
        var context = new TokenValidatedContext(
            new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), principal, new AuthenticationProperties());

        var exception = await Record.ExceptionAsync(() => CreateEvents().TokenValidated(context));

        Assert.Null(exception);
    }

    [Fact]
    public async Task TokenValidated_ResolverAllows_AddsReturnedClaims()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var resolver = new FakeEntraInternalUserResolver(EntraInternalUserResolution.Allow(new Dictionary<string, string>
        {
            ["fullName"] = "Alice User",
            ["resolvedRoles"] = "Admin,Distributions"
        }));
        var context = CreateTokenValidatedContext(principal, resolver);

        await CreateEvents().TokenValidated(context);

        Assert.Contains(identity.Claims, c => c.Type == "fullName" && c.Value == "Alice User");
        Assert.Contains(identity.Claims, c => c.Type == "resolvedRoles" && c.Value == "Admin,Distributions");
        Assert.Same(principal, resolver.ReceivedPrincipal);
        Assert.False(context.Result?.Handled ?? false);
    }

    [Fact]
    public async Task TokenValidated_ResolverDenies_HandlesResponseAndRedirectsToDenialPath()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var resolver = new FakeEntraInternalUserResolver(EntraInternalUserResolution.Deny("/Account/NotPermitted"));
        var context = CreateTokenValidatedContext(principal, resolver);

        await CreateEvents().TokenValidated(context);

        Assert.True(context.Result?.Handled);
        Assert.Equal(StatusCodes.Status302Found, context.HttpContext.Response.StatusCode);
        Assert.Equal("/Account/NotPermitted", context.HttpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task TokenValidated_TrimsBulkyIdTokenClaims_ButKeepsTheOnesTheAppReadsAndTheResolvedOnes()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "Jane Internal"),
            new Claim("oid", "22222222-2222-2222-2222-222222222222"),
            new Claim("preferred_username", "jane@example.com"),
            new Claim("onprem_samaccountname", "jane"),
            new Claim("onprem_domainname", "DEFRA"),
            new Claim("aio", "bulky"),
            new Claim("groups", "g1")
        ]);
        var resolver = new FakeEntraInternalUserResolver(EntraInternalUserResolution.Allow(new Dictionary<string, string> { ["fullName"] = "Jane Internal" }));
        var context = CreateTokenValidatedContext(new ClaimsPrincipal(identity), resolver);

        await CreateEvents().TokenValidated(context);

        Assert.Contains(identity.Claims, c => c.Type == ClaimTypes.Name);
        Assert.Contains(identity.Claims, c => c.Type == "oid");
        Assert.Contains(identity.Claims, c => c.Type == "preferred_username");
        Assert.Contains(identity.Claims, c => c.Type == "fullName");
        // Retained (not just read once here) so EntraCookieEvents can re-invoke
        // IEntraInternalUserResolver.ResolveAsync against the cookie's principal later.
        Assert.Contains(identity.Claims, c => c.Type == "onprem_samaccountname");
        Assert.Contains(identity.Claims, c => c.Type == "onprem_domainname");
        Assert.DoesNotContain(identity.Claims, c => c.Type == "aio");
        Assert.DoesNotContain(identity.Claims, c => c.Type == "groups");
    }

    [Fact]
    public async Task TicketReceived_RemovesTheAccessToken_AndKeepsTheOthers()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "access" },
            new AuthenticationToken { Name = "id_token", Value = "id" },
            new AuthenticationToken { Name = "refresh_token", Value = "refresh" }
        ]);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity()), properties, "entra");
        var context = new TicketReceivedContext(new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), ticket);

        await CreateEvents().TicketReceived(context);

        Assert.Null(properties.GetTokenValue("access_token"));
        Assert.Equal("id", properties.GetTokenValue("id_token"));
        Assert.Equal("refresh", properties.GetTokenValue("refresh_token"));
    }

    [Fact]
    public async Task RemoteFailure_HandlesResponseAndRedirectsToGenericErrorPage()
    {
        var httpContext = new DefaultHttpContext();
        var context = new RemoteFailureContext(httpContext, CreateScheme(), new OpenIdConnectOptions(), new InvalidOperationException("boom"));

        await CreateEvents().RemoteFailure(context);

        Assert.True(context.Result.Handled);
        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal("/Home/Error", httpContext.Response.Headers.Location.ToString());
    }
}
