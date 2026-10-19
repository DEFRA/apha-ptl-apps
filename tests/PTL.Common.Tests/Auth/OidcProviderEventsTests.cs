using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PTL.Common.Auth;

namespace PTL.Common.Tests.Auth;

public class OidcProviderEventsTests
{
    private sealed record TestResolution(bool IsAllowed, string? DenialRedirectPath = null, IReadOnlyDictionary<string, string>? Claims = null)
        : IIdentityResolution;

    private sealed class FakeResolver(TestResolution resolution)
    {
        public ClaimsPrincipal? ReceivedPrincipal { get; private set; }

        public Task<TestResolution> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
        {
            ReceivedPrincipal = principal;
            return Task.FromResult(resolution);
        }
    }

    private sealed class TestEvents(ILogger logger, Action<ClaimsIdentity>? mapClaims = null)
        : OidcProviderEvents<FakeResolver, TestResolution>(logger, "Test", OpenIdConnectEventHelpers.RetainedClaims())
    {
        protected override void MapClaims(ClaimsPrincipal principal, ClaimsIdentity identity) => mapClaims?.Invoke(identity);

        protected override Task<TestResolution> ResolveAsync(FakeResolver resolver, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
            resolver.ResolveAsync(principal, cancellationToken);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((formatter(state, exception), exception));
    }

    private static AuthenticationScheme CreateScheme() => new("oidc", "oidc", typeof(OpenIdConnectHandler));

    private static TokenValidatedContext CreateContext(ClaimsPrincipal principal, FakeResolver? resolver = null)
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
    public async Task TokenValidated_PrincipalWithoutClaimsIdentity_DoesNothing()
    {
        var mapped = false;
        var context = CreateContext(new ClaimsPrincipal());

        await new TestEvents(new CapturingLogger(), _ => mapped = true).TokenValidated(context);

        Assert.False(mapped);
    }

    [Fact]
    public async Task TokenValidated_NoResolverRegistered_MapsClaimsThenTrimsThoseNotRetained()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user"), new Claim("bulky", "x")]);
        var context = CreateContext(new ClaimsPrincipal(identity));

        await new TestEvents(new CapturingLogger(), id => id.AddClaim(new Claim(ClaimTypes.Email, "a@b.test"))).TokenValidated(context);

        Assert.Contains(identity.Claims, c => c.Type == ClaimTypes.Name);
        Assert.Contains(identity.Claims, c => c.Type == ClaimTypes.Email);
        Assert.DoesNotContain(identity.Claims, c => c.Type == "bulky");
        Assert.False(context.Result?.Handled ?? false);
    }

    [Fact]
    public async Task TokenValidated_ResolverAllows_PassesMappedPrincipalAndAddsReturnedClaims()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var principal = new ClaimsPrincipal(identity);
        var resolver = new FakeResolver(new TestResolution(true, Claims: new Dictionary<string, string> { ["resolved"] = "yes" }));
        var context = CreateContext(principal, resolver);

        await new TestEvents(new CapturingLogger(), id => id.AddClaim(new Claim(ClaimTypes.Email, "a@b.test"))).TokenValidated(context);

        Assert.Same(principal, resolver.ReceivedPrincipal);
        Assert.Contains(identity.Claims, c => c.Type == "resolved" && c.Value == "yes");
        Assert.Contains(identity.Claims, c => c.Type == ClaimTypes.Email);
    }

    [Fact]
    public async Task TokenValidated_ResolverDenies_RedirectsToDenialPath()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test-user")]);
        var context = CreateContext(new ClaimsPrincipal(identity), new FakeResolver(new TestResolution(false, "/Account/NotPermitted")));

        await new TestEvents(new CapturingLogger()).TokenValidated(context);

        Assert.True(context.Result?.Handled);
        Assert.Equal("/Account/NotPermitted", context.HttpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task TicketReceived_RemovesTheAccessToken()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "access" },
            new AuthenticationToken { Name = "id_token", Value = "id" }
        ]);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity()), properties, "oidc");
        var context = new TicketReceivedContext(new DefaultHttpContext(), CreateScheme(), new OpenIdConnectOptions(), ticket);

        await new TestEvents(new CapturingLogger()).TicketReceived(context);

        Assert.Null(properties.GetTokenValue("access_token"));
        Assert.Equal("id", properties.GetTokenValue("id_token"));
    }

    [Fact]
    public async Task RemoteFailure_LogsTheProviderNameAndRedirectsToGenericErrorPage()
    {
        var logger = new CapturingLogger();
        var failure = new InvalidOperationException("boom");
        var httpContext = new DefaultHttpContext();
        var context = new RemoteFailureContext(httpContext, CreateScheme(), new OpenIdConnectOptions(), failure);

        await new TestEvents(logger).RemoteFailure(context);

        Assert.Equal(("Test authentication failed", failure), Assert.Single(logger.Entries));
        Assert.True(context.Result?.Handled);
        Assert.Equal("/Home/Error", httpContext.Response.Headers.Location.ToString());
    }
}
