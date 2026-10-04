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
    internal static void SetApiBaseUrl() =>
        Environment.SetEnvironmentVariable("Api__BaseUrl", "http://localhost");
}
