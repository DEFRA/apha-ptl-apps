using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PTL.ApiClient.Tests;

/// <summary>
/// Tests for the PtlWebApplicationExtensions shared startup bootstrap.
/// Focuses on the ApiClientServiceCollectionExtensions which is called by AddPtlWebFrontEnd.
/// </summary>
public class PtlWebApplicationExtensionsTests
{
    [Fact]
    public void AddPtlApiClient_WithValidBaseUrl_RegistersHttpClients()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Api:BaseUrl", "http://localhost:8080")
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        ApiClientServiceCollectionExtensions.AddPtlApiClient(services, config);

        // Verify key client registrations
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(IApiClient)));
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(ICustomerApiClient)));
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(IParticipantApiClient)));
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(IContractApiClient)));
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(ISchemeApiClient)));
        Assert.NotNull(services.FirstOrDefault(s => s.ServiceType == typeof(ILookupApiClient)));
    }

    [Fact]
    public void AddPtlApiClient_WithMissingBaseUrl_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder().Build(); // Empty, no Api:BaseUrl

        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ApiClientServiceCollectionExtensions.AddPtlApiClient(services, config));

        Assert.Contains("Api:BaseUrl", ex.Message);
    }

    [Fact]
    public void AddPtlApiClient_WithMalformedUrl_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Api:BaseUrl", "not-a-valid-url")
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ApiClientServiceCollectionExtensions.AddPtlApiClient(services, config));

        Assert.Contains("http://", ex.Message);
    }

    [Fact]
    public void AddPtlApiClient_WithFtpScheme_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Api:BaseUrl", "ftp://invalid-scheme.com")
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ApiClientServiceCollectionExtensions.AddPtlApiClient(services, config));

        Assert.Contains("http://", ex.Message);
    }
}
