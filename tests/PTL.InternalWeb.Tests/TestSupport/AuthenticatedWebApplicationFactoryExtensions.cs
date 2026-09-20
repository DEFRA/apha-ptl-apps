using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace PTL.InternalWeb.Tests.TestSupport;

// Shared by every full-pipeline test that needs to reach a page behind the [Authorize] fallback
// policy without a real Entra ID round-trip.
public static class AuthenticatedWebApplicationFactoryExtensions
{
    public static WebApplicationFactory<Program> WithTestAuthentication(this WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, options => { })));
}
