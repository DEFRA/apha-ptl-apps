using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PTL.InternalWeb.Authentication;
using PTL.InternalWeb.Tests.TestSupport;

namespace PTL.InternalWeb.Tests.Authentication;

public class DistributedDataProtectionExtensionsTests
{
    [Fact]
    public void AddDistributedDataProtection_InDevelopmentWithoutRedis_DoesNotThrowAndUsesLocalKeyRing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(connectionString: null);
        var environment = new FakeHostEnvironment { EnvironmentName = "Development" };

        services.AddDistributedDataProtection(configuration, environment);
        using var provider = services.BuildServiceProvider();

        // Resolves and round-trips without contacting Redis - proves the local file-system
        // provider was registered rather than a Redis-backed one.
        var dataProtectionProvider = provider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtectionProvider.CreateProtector("test-purpose");
        var protectedValue = protector.Protect("hello");
        Assert.Equal("hello", protector.Unprotect(protectedValue));
    }

    [Fact]
    public void AddDistributedDataProtection_OutsideDevelopmentWithoutRedis_Throws()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(connectionString: null);
        var environment = new FakeHostEnvironment { EnvironmentName = "Production" };

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddDistributedDataProtection(configuration, environment));
        Assert.Contains("Redis:ConnectionString", exception.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void AddDistributedDataProtection_WithRedisConfigured_DoesNotThrow(string environmentName)
    {
        var services = new ServiceCollection();
        // abortConnect=false + a tiny timeout keeps this fast and offline-safe even though
        // nothing is actually listening on this port.
        var configuration = BuildConfiguration(connectionString: "localhost:6390,abortConnect=false,connectTimeout=200");
        var environment = new FakeHostEnvironment { EnvironmentName = environmentName };

        var exception = Record.Exception(() => services.AddDistributedDataProtection(configuration, environment));

        Assert.Null(exception);
    }

    private static IConfiguration BuildConfiguration(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = connectionString
            })
            .Build();
}
