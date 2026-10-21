using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PTL.InternalWeb.Tests.TestSupport;

[assembly: HostingStartup(typeof(SignedInUserHostingStartup))]

namespace PTL.InternalWeb.Tests.TestSupport;

// Loaded into every host the tests build (TestHostConfiguration sets ASPNETCORE_HOSTINGSTARTUPASSEMBLIES). It signs
// every request in as a test user holding the gating PTL roles, so the many view-rendering tests keep reaching
// their role-gated controllers. Fixtures that exercise the real cookie/OIDC pipeline set PtlTests:UseRealAuth.
public sealed class SignedInUserHostingStartup : IHostingStartup
{
    public const string UseRealAuthSetting = "PtlTests:UseRealAuth";

    public void Configure(IWebHostBuilder builder) =>
        builder.ConfigureServices((context, services) =>
        {
            if (context.Configuration.GetValue<bool>(UseRealAuthSetting) || services.Any(d => d.ServiceType == typeof(RegistrationMarker)))
            {
                return;
            }

            services.AddSingleton<RegistrationMarker>();
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            });
        });

    // The hosting startup can be applied more than once to the same service collection.
    private sealed class RegistrationMarker;
}
