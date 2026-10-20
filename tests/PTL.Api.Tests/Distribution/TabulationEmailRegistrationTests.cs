using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PTL.Api.BackgroundServices;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Distribution;
using PTL.Core.Notifications;

namespace PTL.Api.Tests.Distribution;

// Program.cs registers the email services and the one hosted scheduler.
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

        // All three flags off: the hosted scheduler returns at once, so no test run ever reaches a real database.
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TabulationEmails:Enabled"] = "false",
                ["PublicationNotifications:Enabled"] = "false",
                ["OnlineOrderEmails:Enabled"] = "false",
            })));
    }

    [Fact]
    public void PendingOrderNotifier_ResolvesFromTheRealContainerAndTheServiceDependsOnIt()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.IsType<PendingOrderNotifier>(scope.ServiceProvider.GetRequiredService<IPendingOrderNotifier>());
        Assert.IsType<PendingOrderService>(scope.ServiceProvider.GetRequiredService<IPendingOrderService>());
    }

    [Fact]
    public void PendingOrderEmailGuard_IsOneSingletonSharedByEveryScope()
    {
        var root = _factory.Services.GetRequiredService<PendingOrderEmailGuard>();
        using var scope = _factory.Services.CreateScope();

        Assert.Same(root, scope.ServiceProvider.GetRequiredService<PendingOrderEmailGuard>());
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
    public void NotificationBackgroundService_IsTheOnlyHostedSchedulerAndIsRegisteredOnce()
    {
        var hosted = _factory.Services.GetServices<IHostedService>().OfType<NotificationBackgroundService>();

        Assert.Single(hosted);
    }

    [Fact]
    public void SchedulerOptions_AreBoundFromConfiguration()
    {
        Assert.False(_factory.Services.GetRequiredService<IOptions<TabulationEmailOptions>>().Value.Enabled);
        Assert.False(_factory.Services.GetRequiredService<IOptions<PublicationNotificationOptions>>().Value.Enabled);
        Assert.False(_factory.Services.GetRequiredService<IOptions<OnlineOrderEmailOptions>>().Value.Enabled);
        Assert.Equal(TimeSpan.FromMinutes(5), _factory.Services.GetRequiredService<IOptions<NotificationSchedulerOptions>>().Value.PollInterval);
    }
}
