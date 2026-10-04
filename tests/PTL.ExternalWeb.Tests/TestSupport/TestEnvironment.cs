using System.Runtime.CompilerServices;

namespace PTL.ExternalWeb.Tests.TestSupport;

// WebApplicationFactory<Program> builds config the same way the real app does (including
// environment variables) but never reads Properties/launchSettings.json - that file only applies
// to `dotnet run`/IDE launch profiles. Api:BaseUrl has no other source now that
// appsettings.Development.json is gitignored (and absent in CI), so every
// WebApplicationFactory-based test would fail with "Configuration value 'Api:BaseUrl' is
// required" without this. Runs once, before any test in this assembly, regardless of test runner.
internal static class TestEnvironment
{
    [ModuleInitializer]
    public static void EnsureApiBaseUrlConfigured() =>
        Environment.SetEnvironmentVariable("Api__BaseUrl", "http://localhost:5252");
}
