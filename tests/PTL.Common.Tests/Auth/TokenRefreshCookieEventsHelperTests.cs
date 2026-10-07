using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PTL.Common.Auth;

namespace PTL.Common.Tests.Auth;

public class TokenRefreshCookieEventsHelperTests
{
    private sealed record FakeTokenRefreshResult(bool Succeeded, string? AccessToken, string? IdToken, string? RefreshToken, DateTimeOffset? ExpiresAt)
        : ITokenRefreshResult
    {
        public static FakeTokenRefreshResult Failed { get; } = new(false, null, null, null, null);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const string CallbackPath = "/signin-oidc";

    private static CookieValidatePrincipalContext CreateContext(AuthenticationProperties properties, out FakeAuthenticationService authService)
    {
        authService = new FakeAuthenticationService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authService);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var ticket = new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(), properties, scheme.Name);
        var context = new CookieValidatePrincipalContext(httpContext, scheme, new CookieAuthenticationOptions(), ticket);

        // The context doesn't surface the exact AuthenticationProperties instance passed into the
        // ticket - copy the token items across directly onto the one it actually exposes.
        foreach (var (key, value) in properties.Items)
        {
            context.Properties.Items[key] = value;
        }

        return context;
    }

    private static AuthenticationProperties CreatePropertiesWithTokens(string expiresAt, string refreshToken)
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "original-access-token" },
            new AuthenticationToken { Name = "id_token", Value = "original-id-token" },
            new AuthenticationToken { Name = "expires_at", Value = expiresAt },
            new AuthenticationToken { Name = "refresh_token", Value = refreshToken }
        ]);
        return properties;
    }

    [Fact]
    public async Task ValidatePrincipalAsync_NoTokenMetadata_LeavesPrincipalUnchanged()
    {
        var context = CreateContext(new AuthenticationProperties(), out _);
        var callCount = 0;

        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context, TimeSpan.FromMinutes(5), CallbackPath, new FakeTimeProvider(Now),
            (_, _, _) => { callCount++; return Task.FromResult<FakeTokenRefreshResult>(FakeTokenRefreshResult.Failed); },
            () => { });

        Assert.False(context.ShouldRenew);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public async Task ValidatePrincipalAsync_NotYetNearExpiry_DoesNotRefresh()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(30).ToString("o"), "refresh-token-value");
        var context = CreateContext(properties, out _);
        var callCount = 0;

        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context, TimeSpan.FromMinutes(5), CallbackPath, new FakeTimeProvider(Now),
            (_, _, _) => { callCount++; return Task.FromResult<FakeTokenRefreshResult>(FakeTokenRefreshResult.Failed); },
            () => { });

        Assert.Equal(0, callCount);
    }

    [Fact]
    public async Task ValidatePrincipalAsync_NearExpiryAndRefreshSucceeds_UpdatesTokensAndRenews()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(2).ToString("o"), "old-refresh-token");
        var context = CreateContext(properties, out _);
        var result = new FakeTokenRefreshResult(true, "new-access", "new-id", "new-refresh", Now.AddHours(1));

        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context, TimeSpan.FromMinutes(5), CallbackPath, new FakeTimeProvider(Now),
            (_, _, _) => Task.FromResult(result),
            () => { });

        Assert.True(context.ShouldRenew);
        Assert.Equal("new-access", context.Properties.GetTokenValue("access_token"));
        Assert.Equal("new-id", context.Properties.GetTokenValue("id_token"));
        Assert.Equal("new-refresh", context.Properties.GetTokenValue("refresh_token"));
    }

    [Fact]
    public async Task ValidatePrincipalAsync_NearExpiryAndRefreshFails_RejectsPrincipalAndSignsOut()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(1).ToString("o"), "old-refresh-token");
        var context = CreateContext(properties, out var authService);
        var loggedFailure = false;

        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context, TimeSpan.FromMinutes(5), CallbackPath, new FakeTimeProvider(Now),
            (_, _, _) => Task.FromResult<FakeTokenRefreshResult>(FakeTokenRefreshResult.Failed),
            () => loggedFailure = true);

        Assert.False(context.ShouldRenew);
        Assert.True(loggedFailure);
        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, authService.SignOutSchemes);
    }

    [Fact]
    public async Task ValidatePrincipalAsync_RefreshSuccessWithNoNewRefreshToken_KeepsOriginalRefreshToken()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(1).ToString("o"), "original-refresh-token");
        var context = CreateContext(properties, out _);
        var result = new FakeTokenRefreshResult(true, "new-access", null, null, Now.AddHours(1));

        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context, TimeSpan.FromMinutes(5), CallbackPath, new FakeTimeProvider(Now),
            (_, _, _) => Task.FromResult(result),
            () => { });

        Assert.Equal("original-refresh-token", context.Properties.GetTokenValue("refresh_token"));
    }

    [Fact]
    public async Task ValidatePrincipalAsync_BuildsRedirectUriFromRequestAndCallbackPath()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(1).ToString("o"), "old-refresh-token");
        var context = CreateContext(properties, out _);
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("app.test");
        string? capturedRedirectUri = null;

        await TokenRefreshCookieEventsHelper.ValidatePrincipalAsync(
            context, TimeSpan.FromMinutes(5), CallbackPath, new FakeTimeProvider(Now),
            (_, redirectUri, _) => { capturedRedirectUri = redirectUri; return Task.FromResult<FakeTokenRefreshResult>(FakeTokenRefreshResult.Failed); },
            () => { });

        Assert.Equal("https://app.test/signin-oidc", capturedRedirectUri);
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public List<string?> SignOutSchemes { get; } = [];

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOutSchemes.Add(scheme);
            return Task.CompletedTask;
        }
    }
}
