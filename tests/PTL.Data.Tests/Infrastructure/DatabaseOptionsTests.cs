using Microsoft.Extensions.Configuration;
using PTL.Data.Infrastructure;

namespace PTL.Data.Tests.Infrastructure;

public class DatabaseOptionsTests
{
    [Fact]
    public void ToConnectionString_UsesUserCredentials_WhenNotIntegratedSecurity()
    {
        var options = new DatabaseOptions("myhost", "mydb", "myuser", "mypassword", TrustServerCertificate: true);

        var connectionString = options.ToConnectionString();

        Assert.Contains("myhost", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mydb", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("myuser", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Integrated Security", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToConnectionString_UsesIntegratedSecurity_WhenRequested()
    {
        var options = new DatabaseOptions("myhost", "mydb", string.Empty, string.Empty, TrustServerCertificate: false, IntegratedSecurity: true);

        var connectionString = options.ToConnectionString();

        Assert.Contains("Integrated Security", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User ID", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequireFromConfiguration_Throws_WhenHostOrNameMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Name"] = "db" })
            .Build();

        Assert.Throws<InvalidOperationException>(() => DatabaseOptions.RequireFromConfiguration(configuration));
    }

    [Fact]
    public void RequireFromConfiguration_Throws_WhenCredentialsMissingAndNotIntegratedSecurity()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = "host",
                ["Database:Name"] = "db",
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => DatabaseOptions.RequireFromConfiguration(configuration));
    }

    [Fact]
    public void RequireFromConfiguration_Succeeds_WhenIntegratedSecurity_AndNoCredentials()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = "host",
                ["Database:Name"] = "db",
                ["Database:IntegratedSecurity"] = "true",
            })
            .Build();

        var options = DatabaseOptions.RequireFromConfiguration(configuration);

        Assert.Equal("host", options.Host);
        Assert.Equal("db", options.Name);
        Assert.True(options.IntegratedSecurity);
    }

    [Fact]
    public void RequireFromConfiguration_ReadsTrustServerCertificate_WhenSet()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = "host",
                ["Database:Name"] = "db",
                ["Database:User"] = "user",
                ["Database:Password"] = "pw",
                ["Database:TrustServerCertificate"] = "true",
            })
            .Build();

        var options = DatabaseOptions.RequireFromConfiguration(configuration);

        Assert.True(options.TrustServerCertificate);
        Assert.Equal("user", options.User);
        Assert.Equal("pw", options.Password);
    }
}
