using System.Runtime.CompilerServices;

namespace PTL.Api.Tests.TestSupport;

internal static class TestHostConfiguration
{
    // WebApplicationFactory boots the real Program, whose StartupChecks fail fast without
    // Database:Host/Name and HealthCheck:ReadinessKey. Locally those live in a gitignored
    // appsettings.Development.json, which the test host (Production environment) never reads.
    // The host is deliberately unreachable - no test here talks to a real database.
#pragma warning disable CA2255 // ModuleInitializer is the only hook that runs before the first host build.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void SetRequiredStartupConfiguration()
    {
        Environment.SetEnvironmentVariable("Database__Host", "invalid-host-for-tests,1433");
        Environment.SetEnvironmentVariable("Database__Name", "ProficiencyTesting");
        Environment.SetEnvironmentVariable("Database__IntegratedSecurity", "true");
        Environment.SetEnvironmentVariable("Database__TrustServerCertificate", "true");
        Environment.SetEnvironmentVariable("HealthCheck__ReadinessKey", "local-dev-readiness-key");
    }
}
