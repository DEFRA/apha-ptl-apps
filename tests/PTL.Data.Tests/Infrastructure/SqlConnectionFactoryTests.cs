using Microsoft.Extensions.Configuration;
using PTL.Data.Infrastructure;

namespace PTL.Data.Tests.Infrastructure;

public class SqlConnectionFactoryTests
{
    [Fact]
    public void CreateConnection_ReturnsSqlConnection_WithComposedConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Host"] = "myhost",
                ["Database:Name"] = "mydb",
                ["Database:User"] = "myuser",
                ["Database:Password"] = "mypassword",
            })
            .Build();

        using var connection = new SqlConnectionFactory(configuration).CreateConnection();

        Assert.Contains("myhost", connection.ConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mydb", connection.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateConnection_Throws_WhenConfigurationIncomplete()
    {
        var configuration = new ConfigurationBuilder().Build();

        var factory = new SqlConnectionFactory(configuration);

        Assert.Throws<InvalidOperationException>(() => factory.CreateConnection());
    }
}
