using System.Runtime.CompilerServices;

namespace PTL.InternalWeb.Tests.TestSupport;

internal static class TestHostConfiguration
{
    // WebApplicationFactory boots the real Program against the app's own appsettings.json, which
    // deliberately carries no environment-specific API URL - and AddPtlApiClient fails fast without
    // one. The value is never called: every integration test replaces the typed clients with fakes.
#pragma warning disable CA2255 // ModuleInitializer is the only hook that runs before the first host build.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void SetApiBaseUrl()
    {
        Environment.SetEnvironmentVariable("Api__BaseUrl", "http://localhost");

        // EntraOptions.ValidateOnStart() fails host startup without these - every integration test
        // replaces IEntraInternalUserResolver's dependencies with fakes, so the actual values never
        // matter, only that the host can start at all.
        Environment.SetEnvironmentVariable("Entra__TenantId", "00000000-0000-0000-0000-000000000000");
        Environment.SetEnvironmentVariable("Entra__ClientId", "test-client-id");
        Environment.SetEnvironmentVariable("Entra__ClientSecret", "test-client-secret");
    }
}
