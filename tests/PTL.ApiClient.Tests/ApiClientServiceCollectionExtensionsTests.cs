using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PTL.ApiClient.Tests;

public class ApiClientServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPtlApiClient_WithoutBaseUrl_Throws()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => services.AddPtlApiClient(configuration));
    }

    [Fact]
    public void AddPtlApiClient_WithBaseUrl_RegistersApiClient()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Api:BaseUrl"] = "http://localhost:5252" })
            .Build();

        services.AddPtlApiClient(configuration);
        var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IApiClient>();
        Assert.IsType<ApiClient>(client);
    }
}
