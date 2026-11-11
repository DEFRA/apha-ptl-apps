using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Minimal IAuthenticationService so controller unit tests can call AuthenticateAsync/SignInAsync/SignOutAsync without a full
// authentication pipeline. Records what was signed in and out, so tests can assert what a controller action actually did.
internal sealed class FakeAuthenticationService : IAuthenticationService
{
    public List<string?> SignOutSchemes { get; } = [];

    // What AuthenticateAsync returns; none until a test sets one.
    public AuthenticateResult AuthenticateResult { get; set; } = AuthenticateResult.NoResult();

    public List<(string? Scheme, ClaimsPrincipal Principal, AuthenticationProperties? Properties)> SignIns { get; } = [];

    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        Task.FromResult(AuthenticateResult);

    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        Task.CompletedTask;

    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
    {
        SignIns.Add((scheme, principal, properties));
        return Task.CompletedTask;
    }

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        SignOutSchemes.Add(scheme);
        return Task.CompletedTask;
    }
}
