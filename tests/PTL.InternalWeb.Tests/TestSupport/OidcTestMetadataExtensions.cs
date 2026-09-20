using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace PTL.InternalWeb.Tests.TestSupport;

// A real OpenIdConnectHandler challenge/sign-out normally fetches the provider's metadata
// document over the network on first use. Tests must never depend on reaching a real Entra ID
// tenant (or the network at all), so this supplies a static, fake metadata document instead -
// this is the officially supported way to exercise OIDC challenge/sign-out behaviour offline.
public static class OidcTestMetadataExtensions
{
    public const string FakeAuthorizationEndpoint = "https://login.example.test/authorize";
    public const string FakeEndSessionEndpoint = "https://login.example.test/logout";

    public static WebApplicationFactory<Program> WithoutRealOidcDiscovery(this WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://login.example.test/",
                    AuthorizationEndpoint = FakeAuthorizationEndpoint,
                    TokenEndpoint = "https://login.example.test/token",
                    EndSessionEndpoint = FakeEndSessionEndpoint,
                    JwksUri = "https://login.example.test/keys"
                };

                // Setting Options.Configuration alone has no effect once the handler has already
                // built its real ConfigurationManager (which happens during the framework's own
                // PostConfigure, registered by AddOpenIdConnect before this one runs) - it must be
                // replaced outright with a manager that never calls out to the network.
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            })));
}

