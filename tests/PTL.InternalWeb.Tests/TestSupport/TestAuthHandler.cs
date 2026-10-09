using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.InternalWeb.Features.Account;

namespace PTL.InternalWeb.Tests.TestSupport;

// Always-succeeds authentication handler for WebApplicationFactory-based integration tests -
// stands in for a real Entra ID sign-in so tests can assert on SystemAdministrationController's
// [Authorize(Policy = SystemAdministrationPolicy.Name)] gate without a live Entra tenant. Register
// as the default scheme (see SystemAdministrationRouteSmokeTests) to simulate a signed-in Admin;
// omit it from a factory's service overrides to simulate an anonymous request instead.
public sealed class TestAuthHandler(IOptionsMonitor<TestAuthHandlerOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<TestAuthHandlerOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(InternalUserClaimTypes.InternalUserId, Options.UserId.ToString()),
            new Claim(InternalUserClaimTypes.FullName, "Test Admin"),
            new Claim(InternalUserClaimTypes.ResolvedRoles, Options.ResolvedRoles)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public sealed class TestAuthHandlerOptions : AuthenticationSchemeOptions
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string ResolvedRoles { get; set; } = "Admin";
}
