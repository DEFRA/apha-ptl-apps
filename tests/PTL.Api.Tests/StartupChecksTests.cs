using Microsoft.Extensions.Configuration;
using PTL.Api.Infrastructure;

namespace PTL.Api.Tests;

public class StartupChecksTests
{
    [Fact]
    public void RequireDatabaseOptions_Throws_WhenAnyValueMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = "host",
                ["Database:Name"] = "db"
                // User and Password deliberately omitted
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => StartupChecks.RequireDatabaseOptions(configuration));
    }

    [Fact]
    public void RequireDatabaseOptions_ReturnsOptions_WhenAllValuesPresent()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = "host",
                ["Database:Name"] = "db",
                ["Database:User"] = "user",
                ["Database:Password"] = "pw"
            })
            .Build();

        var options = StartupChecks.RequireDatabaseOptions(configuration);

        Assert.Equal("host", options.Host);
        Assert.Equal("db", options.Name);
        Assert.Equal("user", options.User);
        Assert.Equal("pw", options.Password);
        Assert.False(options.TrustServerCertificate);
    }

    [Fact]
    public void RequireDatabaseOptions_ReadsTrustServerCertificate_WhenSet()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = "host",
                ["Database:Name"] = "db",
                ["Database:User"] = "user",
                ["Database:Password"] = "pw",
                ["Database:TrustServerCertificate"] = "true"
            })
            .Build();

        var options = StartupChecks.RequireDatabaseOptions(configuration);

        Assert.True(options.TrustServerCertificate);
    }

    [Fact]
    public void RequireReadinessKey_Throws_WhenMissing()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => StartupChecks.RequireReadinessKey(configuration));
    }

    [Fact]
    public void RequireReadinessKey_ReturnsValue_WhenPresent()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HealthCheck:ReadinessKey"] = "abc123"
            })
            .Build();

        var result = StartupChecks.RequireReadinessKey(configuration);

        Assert.Equal("abc123", result);
    }
}
