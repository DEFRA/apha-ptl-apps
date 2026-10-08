using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PTL.Auth.Entra;

namespace PTL.InternalWeb.Tests.TestSupport;

/// <summary>
/// Supplies dummy Entra ID configuration (so <c>EntraOptionsValidator</c> doesn't fail startup) and
/// a static, network-free OpenID Connect discovery document (so the handler never makes a real HTTP
/// call), so integration tests can exercise the auth pipeline without a real Entra ID tenant. Also
/// adds a test-only sign-in endpoint so tests can reach authenticated-only view branches (e.g. the
/// shared header's "Signed in as" state and the sign-out flow) without simulating a full Entra ID
/// redirect/callback handshake.
/// </summary>
public sealed class PtlInternalWebTestFactory : WebApplicationFactory<Program>
{
    public const string FakeAuthorizationEndpoint = "https://entra.test/oauth2/v2.0/authorize";
    public const string FakeEndSessionEndpoint = "https://entra.test/logout";
    public const string TestSignInPath = "/__test/sign-in";
    public const string TestIdToken = "test-id-token";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Entra:TenantId"] = "00000000-0000-0000-0000-000000000000",
            ["Entra:ClientId"] = "test-client-id",
            ["Entra:ClientSecret"] = "test-client-secret"
        }));

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<OpenIdConnectOptions>(
                EntraAuthenticationDefaults.AuthenticationScheme,
                options =>
                {
                    var configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = "https://entra.test/",
                        AuthorizationEndpoint = FakeAuthorizationEndpoint,
                        TokenEndpoint = "https://entra.test/oauth2/v2.0/token",
                        JwksUri = "https://entra.test/discovery/v2.0/keys",
                        EndSessionEndpoint = FakeEndSessionEndpoint
                    };

                    // Explicitly overrides whatever ConfigurationManager the built-in
                    // OpenIdConnectPostConfigureOptions set up from Authority, guaranteeing no real
                    // HTTP discovery call happens in tests regardless of PostConfigure registration order.
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });

            services.AddSingleton<IStartupFilter, TestSignInStartupFilter>();
        });
    }

    // The app cookie is Secure-only (CookieSecurePolicy.Always) - TestServer's default http://
    // client scheme means the CookieContainer silently drops it between requests unless every
    // request through this client is treated as HTTPS.
    protected override void ConfigureClient(HttpClient client) => client.BaseAddress = new Uri("https://localhost");

    private sealed class TestSignInStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == TestSignInPath)
                {
                    var identity = new ClaimsIdentity(
                        [new Claim(ClaimTypes.Name, "test-user")],
                        CookieAuthenticationDefaults.AuthenticationScheme);
                    var properties = new AuthenticationProperties();
                    properties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = TestIdToken }]);
                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties);
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }

                await nextMiddleware();
            });

            next(app);
        };
    }
}
