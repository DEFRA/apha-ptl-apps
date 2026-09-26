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
using PTL.Auth.Cidm;

namespace PTL.ExternalWeb.Tests.TestSupport;

/// <summary>
/// Supplies dummy CIDM configuration (so <c>CidmOptionsValidator</c> doesn't fail startup) and a
/// static, network-free OpenID Connect discovery document (so the handler never makes a real HTTP
/// call), so integration tests can exercise the auth pipeline without a real DEFRA CIDM tenant. Also
/// adds a test-only sign-in endpoint so tests can reach authenticated-only view branches (e.g. the
/// shared header's "Signed in as" state and the sign-out flow) without simulating a full CIDM
/// redirect/callback handshake.
/// </summary>
public sealed class PtlExternalWebTestFactory : WebApplicationFactory<Program>
{
    public const string FakeAuthorizationEndpoint = "https://cidm.test/oauth2/v2.0/authorize";
    public const string FakeEndSessionEndpoint = "https://cidm.test/signout";
    public const string TestSignInPath = "/__test/sign-in";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cidm:Address"] = "https://cidm.test/idphub/b2c",
            ["Cidm:Policy"] = "b2c_1a_test_signupsignin",
            ["Cidm:ClientId"] = "test-client-id",
            ["Cidm:ClientSecret"] = "test-client-secret",
            ["Cidm:ServiceId"] = "test-service-id"
        }));

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<OpenIdConnectOptions>(
                CidmAuthenticationDefaults.AuthenticationScheme,
                options =>
                {
                    var configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = "https://cidm.test/",
                        AuthorizationEndpoint = FakeAuthorizationEndpoint,
                        TokenEndpoint = "https://cidm.test/oauth2/v2.0/token",
                        JwksUri = "https://cidm.test/discovery/v2.0/keys",
                        EndSessionEndpoint = FakeEndSessionEndpoint
                    };

                    // Explicitly overrides whatever ConfigurationManager the built-in OpenIdConnectPostConfigureOptions
                    // set up from MetadataAddress, guaranteeing no real HTTP discovery call happens in tests
                    // regardless of PostConfigure registration order.
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
                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }

                await nextMiddleware();
            });

            next(app);
        };
    }
}
