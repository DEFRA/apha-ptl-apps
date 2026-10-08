using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PTL.Common.Auth;

namespace PTL.Api.Tests.Common;

public class TokenRefreshCookieEventsHelperTests
{
    private const string SchemeName = "TestCookieScheme";

    [Fact]
    public async Task ValidatePrincipalAsync_WithoutTokenMetadata_DoesNotRenew()
    {
        var (context, refreshCalled) = CreateContextAndTracker(expiresAt: null, refreshToken: null);

        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context, TimeSpan.FromMinutes(5), "/signin-oidc", TimeProvider.System,
            (_, _, _) => { refreshCalled(); return Task.FromResult(new FakeTokenRefreshResult(true)); },
            () => { });

        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public async Task ValidatePrincipalAsync_WithMalformedExpiresAt_DoesNotRenew()
    {
        var (context, _) = CreateContextAndTracker(expiresAt: "not-a-date", refreshToken: "rt");

        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context, TimeSpan.FromMinutes(5), "/signin-oidc", TimeProvider.System,
            (_, _, _) => Task.FromResult(new FakeTokenRefreshResult(true)),
            () => { });

        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public async Task ValidatePrincipalAsync_WhenNotYetNearExpiry_DoesNotCallRefresh()
    {
        var called = false;
        var (context, _) = CreateContextAndTracker(
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(10).ToString("o"), refreshToken: "rt");

        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context, TimeSpan.FromMinutes(1), "/signin-oidc", TimeProvider.System,
            (_, _, _) => { called = true; return Task.FromResult(new FakeTokenRefreshResult(true)); },
            () => { });

        Assert.False(called);
        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public async Task ValidatePrincipalAsync_WhenRefreshFails_RejectsPrincipalAndSignsOut()
    {
        var failureLogged = false;
        var (context, _) = CreateContextAndTracker(
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(10).ToString("o"), refreshToken: "rt");

        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context, TimeSpan.FromMinutes(1), "/signin-oidc", TimeProvider.System,
            (_, _, _) => Task.FromResult(new FakeTokenRefreshResult(false)),
            () => failureLogged = true);

        Assert.True(failureLogged);
        Assert.Null(context.Principal);
        Assert.False(context.ShouldRenew);
    }

    [Fact]
    public async Task ValidatePrincipalAsync_WhenRefreshSucceedsWithNewIdAndRefreshTokens_UpdatesAllTokens()
    {
        var (context, _) = CreateContextAndTracker(
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(10).ToString("o"), refreshToken: "old-rt");
        var newExpiry = DateTimeOffset.UtcNow.AddHours(1);

        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context, TimeSpan.FromMinutes(1), "/signin-oidc", TimeProvider.System,
            (_, _, _) => Task.FromResult(new FakeTokenRefreshResult(true, "new-at", "new-id", "new-rt", newExpiry)),
            () => { });

        Assert.True(context.ShouldRenew);
        Assert.Equal("new-at", context.Properties.GetTokenValue("access_token"));
        Assert.Equal("new-id", context.Properties.GetTokenValue("id_token"));
        Assert.Equal("new-rt", context.Properties.GetTokenValue("refresh_token"));
    }

    [Fact]
    public async Task ValidatePrincipalAsync_WhenRefreshSucceedsWithoutNewIdOrRefreshToken_FallsBackToOriginalRefreshToken()
    {
        var (context, _) = CreateContextAndTracker(
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(10).ToString("o"), refreshToken: "old-rt");
        var newExpiry = DateTimeOffset.UtcNow.AddHours(1);

        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context, TimeSpan.FromMinutes(1), "/signin-oidc", TimeProvider.System,
            (_, _, _) => Task.FromResult(new FakeTokenRefreshResult(true, "new-at", idToken: null, refreshToken: null, newExpiry)),
            () => { });

        Assert.True(context.ShouldRenew);
        Assert.Equal("old-rt", context.Properties.GetTokenValue("refresh_token"));
    }

    private static (CookieValidatePrincipalContext Context, Action MarkRefreshCalled) CreateContextAndTracker(string? expiresAt, string? refreshToken)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication().AddCookie(SchemeName);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var scheme = new AuthenticationScheme(SchemeName, null, typeof(CookieAuthenticationHandler));
        var options = new CookieAuthenticationOptions();

        var properties = new AuthenticationProperties();
        var tokens = new List<AuthenticationToken>
        {
            new() { Name = "access_token", Value = "original-at" },
            new() { Name = "id_token", Value = "original-id" }
        };
        if (refreshToken is not null)
        {
            tokens.Add(new AuthenticationToken { Name = "refresh_token", Value = refreshToken });
        }
        if (expiresAt is not null)
        {
            tokens.Add(new AuthenticationToken { Name = "expires_at", Value = expiresAt });
        }
        properties.StoreTokens(tokens);

        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity("Cookies"));
        var ticket = new AuthenticationTicket(principal, properties, SchemeName);

        var context = new CookieValidatePrincipalContext(httpContext, scheme, options, ticket);
        return (context, () => { });
    }

    private sealed class FakeTokenRefreshResult(
        bool succeeded,
        string? accessToken = null,
        string? idToken = null,
        string? refreshToken = null,
        DateTimeOffset? expiresAt = null) : ITokenRefreshResult
    {
        public bool Succeeded => succeeded;
        public string? AccessToken => accessToken;
        public string? IdToken => idToken;
        public string? RefreshToken => refreshToken;
        public DateTimeOffset? ExpiresAt => expiresAt ?? DateTimeOffset.UtcNow.AddHours(1);
    }
}
