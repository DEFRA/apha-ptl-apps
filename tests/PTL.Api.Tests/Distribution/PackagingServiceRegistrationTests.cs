using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Proves Program.cs registers everything the Sample Packaging services depend on - a missing
// registration only shows up as a 500 on the first real Save/Print otherwise.
public class PackagingServiceRegistrationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void PackagingServices_ResolveFromTheRealContainer()
    {
        using var scope = factory.Services.CreateScope();

        Assert.IsType<PackagingService>(scope.ServiceProvider.GetRequiredService<IPackagingService>());
        Assert.IsType<PackagingOutputService>(scope.ServiceProvider.GetRequiredService<IPackagingOutputService>());
        Assert.IsType<DistributionPostedNotifier>(scope.ServiceProvider.GetRequiredService<IDistributionPostedNotifier>());
        Assert.Same(TimeProvider.System, scope.ServiceProvider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void ResultsReceiptService_ResolvesFromTheRealContainer()
    {
        using var scope = factory.Services.CreateScope();

        Assert.IsType<ResultsReceiptService>(scope.ServiceProvider.GetRequiredService<IResultsReceiptService>());
    }

    [Fact]
    public void ActualResultsService_ResolvesFromTheRealContainer()
    {
        using var scope = factory.Services.CreateScope();

        Assert.IsType<ActualResultsService>(scope.ServiceProvider.GetRequiredService<IActualResultsService>());
    }

    [Fact]
    public void ExternalOptions_AreBoundFromAppSettings()
    {
        using var scope = factory.Services.CreateScope();

        var external = scope.ServiceProvider.GetRequiredService<IOptions<PTL.Core.Configuration.ExternalOptions>>().Value;

        Assert.Equal("http://localhost:5215/", external.AppUrl);
    }
}
