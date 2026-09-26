using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Minimal no-op IAuthenticationService so controller unit tests can call SignInAsync/SignOutAsync
// without a full authentication pipeline. Records the schemes SignOutAsync was called with, so
// tests can assert which schemes a controller action actually signed out of.
internal sealed class FakeAuthenticationService : IAuthenticationService
{
    public List<string?> SignOutSchemes { get; } = [];

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        Task.FromResult(AuthenticateResult.NoResult());

    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        SignOutSchemes.Add(scheme);
        return Task.CompletedTask;
    }
}
