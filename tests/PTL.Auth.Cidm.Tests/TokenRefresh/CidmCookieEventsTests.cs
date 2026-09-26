using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PTL.Auth.Cidm.Options;
using PTL.Auth.Cidm.TokenRefresh;

namespace PTL.Auth.Cidm.Tests.TokenRefresh;

public class CidmCookieEventsTests
{
    private static readonly CidmOptions TestCidmOptions = new()
    {
        Address = "https://cidm.test/idphub/b2c",
        Policy = "b2c_1a_test_signupsignin",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        ServiceId = "service-id-value",
        RefreshBeforeExpiry = TimeSpan.FromMinutes(5)
    };

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static CidmCookieEvents CreateEvents(ICidmTokenRefreshService refreshService) =>
        new(refreshService, Microsoft.Extensions.Options.Options.Create(TestCidmOptions), new FakeTimeProvider(Now), NullLogger<CidmCookieEvents>.Instance);

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
        // Mirrors what OpenIdConnectHandler's SaveTokens=true actually registers at sign-in time -
        // UpdateTokenValue is a no-op for any name that wasn't part of the initial token set.
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
    public async Task ValidatePrincipal_NoTokenMetadata_LeavesPrincipalUnchanged()
    {
        var context = CreateContext(new AuthenticationProperties(), out _);
        var refreshService = new FakeTokenRefreshService(CidmTokenRefreshResult.Failed);

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.False(context.ShouldRenew);
        Assert.Equal(0, refreshService.CallCount);
    }

    [Fact]
    public async Task ValidatePrincipal_NotYetNearExpiry_DoesNotRefresh()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(30).ToString("o"), "refresh-token-value");
        var context = CreateContext(properties, out _);
        var refreshService = new FakeTokenRefreshService(CidmTokenRefreshResult.Failed);

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.Equal(0, refreshService.CallCount);
    }

    [Fact]
    public async Task ValidatePrincipal_NearExpiryAndRefreshSucceeds_UpdatesTokensAndRenews()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(2).ToString("o"), "old-refresh-token");
        var context = CreateContext(properties, out _);
        var refreshService = new FakeTokenRefreshService(new CidmTokenRefreshResult(true, "new-access", "new-id", "new-refresh", Now.AddHours(1)));

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.True(context.ShouldRenew);
        Assert.Equal("new-access", context.Properties.GetTokenValue("access_token"));
        Assert.Equal("new-id", context.Properties.GetTokenValue("id_token"));
        Assert.Equal("new-refresh", context.Properties.GetTokenValue("refresh_token"));
    }

    [Fact]
    public async Task ValidatePrincipal_NearExpiryAndRefreshFails_RejectsPrincipalAndSignsOut()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(1).ToString("o"), "old-refresh-token");
        var context = CreateContext(properties, out var authService);
        var refreshService = new FakeTokenRefreshService(CidmTokenRefreshResult.Failed);

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.False(context.ShouldRenew);
        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, authService.SignOutSchemes);
    }

    [Fact]
    public async Task ValidatePrincipal_RefreshSuccessWithNoNewRefreshToken_KeepsOriginalRefreshToken()
    {
        var properties = CreatePropertiesWithTokens(Now.AddMinutes(1).ToString("o"), "original-refresh-token");
        var context = CreateContext(properties, out _);
        var refreshService = new FakeTokenRefreshService(new CidmTokenRefreshResult(true, "new-access", null, null, Now.AddHours(1)));

        await CreateEvents(refreshService).ValidatePrincipal(context);

        Assert.Equal("original-refresh-token", context.Properties.GetTokenValue("refresh_token"));
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeTokenRefreshService(CidmTokenRefreshResult result) : ICidmTokenRefreshService
    {
        public int CallCount { get; private set; }

        public Task<CidmTokenRefreshResult> RefreshAsync(string refreshToken, string redirectUri, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
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
