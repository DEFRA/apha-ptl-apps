using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PTL.Api.BackgroundServices;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Program.cs registers the Test Consultant email services and the hosted scheduler.
public class TabulationEmailRegistrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TabulationEmailRegistrationTests(WebApplicationFactory<Program> factory)
    {
        // Startup checks read these before the host is built (see HealthEndpointsTests).
        Environment.SetEnvironmentVariable("Database__Host", "localhost");
        Environment.SetEnvironmentVariable("Database__Name", "ProficiencyTesting");
        Environment.SetEnvironmentVariable("Database__IntegratedSecurity", "true");
        Environment.SetEnvironmentVariable("Database__TrustServerCertificate", "true");
        Environment.SetEnvironmentVariable("HealthCheck__ReadinessKey", "local-dev-readiness-key");

        // Both flags off: the hosted scheduler returns at once, so no test run ever reaches a real database.
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TabulationEmails:Enabled"] = "false",
                ["PublicationNotifications:Enabled"] = "false",
            })));
    }

    [Fact]
    public void TabulationEmailService_ResolvesFromTheRealContainer()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.IsType<TabulationEmailService>(scope.ServiceProvider.GetRequiredService<ITabulationEmailService>());
    }

    [Fact]
    public void TabulationPublishedNotifier_ResolvesFromTheRealContainer()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.IsType<TabulationPublishedNotifier>(scope.ServiceProvider.GetRequiredService<ITabulationPublishedNotifier>());
    }

    [Fact]
    public void TabulationSendService_ResolvesWithItsEmailDependency()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.IsType<TabulationSendService>(scope.ServiceProvider.GetRequiredService<ITabulationSendService>());
    }

    [Fact]
    public void TabulationEmailGuard_IsOneSingletonSharedByEveryScope()
    {
        var root = _factory.Services.GetRequiredService<TabulationEmailGuard>();
        using var first = _factory.Services.CreateScope();
        using var second = _factory.Services.CreateScope();

        Assert.Same(root, first.ServiceProvider.GetRequiredService<TabulationEmailGuard>());
        Assert.Same(root, second.ServiceProvider.GetRequiredService<TabulationEmailGuard>());
    }

    [Fact]
    public void TabulationEmailBackgroundService_IsRegisteredAsAHostedService()
    {
        var hosted = _factory.Services.GetServices<IHostedService>().OfType<TabulationEmailBackgroundService>();

        Assert.Single(hosted);
    }

    [Fact]
    public void SchedulerOptions_AreBoundFromConfiguration()
    {
        Assert.False(_factory.Services.GetRequiredService<IOptions<TabulationEmailOptions>>().Value.Enabled);
        Assert.False(_factory.Services.GetRequiredService<IOptions<PublicationNotificationOptions>>().Value.Enabled);
    }
}
