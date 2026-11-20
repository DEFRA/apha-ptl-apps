using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Common.Correlation;

namespace PTL.ApiClient.Tests.LoadTesting;

public class LoadTestingServiceCollectionExtensionsTests
{
    private const string BypassedSignIn = "Entra ID is bypassed by the username-only sign-in";

    private static IConfiguration Configuration(string? apiBaseUrl = "http://ptl-api:8080", string? enabled = null, string? expiresOn = "2027-01-31") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:BaseUrl"] = apiBaseUrl,
                ["LoadTesting:Enabled"] = enabled,
                ["LoadTesting:ExpiresOn"] = expiresOn,
            })
            .Build();

    private static ServiceProvider BuildProvider(IConfiguration configuration, CapturingLoggerProvider? logs = null, TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(timeProvider ?? FixedTimeProvider.BeforeExpiry);
        services.AddLogging(builder =>
        {
            if (logs is not null)
            {
                builder.AddProvider(logs);
            }
        });

        // Registered by AddPtlApiClient in the apps.
        services.AddHttpContextAccessor();
        services.AddTransient<CorrelationIdDelegatingHandler>();

        services.AddPtlLoadTesting(configuration, BypassedSignIn);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddPtlLoadTesting_WithoutApiBaseUrl_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPtlLoadTesting(Configuration(apiBaseUrl: null), BypassedSignIn));

        Assert.Contains("Api:BaseUrl", exception.Message);
    }

    [Fact]
    public void AddPtlLoadTesting_RegistersTheLookupClientAgainstTheConfiguredApiBaseUrl()
    {
        using var provider = BuildProvider(Configuration());

        Assert.IsType<LoadTestingLookupClient>(provider.GetRequiredService<ILoadTestingLookupClient>());
        var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ILoadTestingLookupClient));
        Assert.Equal(new Uri("http://ptl-api:8080"), httpClient.BaseAddress);
    }

    [Fact]
    public void AddPtlLoadTesting_AddsTheLoginRedirectFilterGlobally()
    {
        using var provider = BuildProvider(Configuration());

        var filters = provider.GetRequiredService<IOptions<MvcOptions>>().Value.Filters;

        Assert.Contains(filters, filter => filter is TypeFilterAttribute { ImplementationType: var type } && type == typeof(LoadTestingLoginRedirectFilter));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void AddPtlLoadTesting_BindsTheSwitchAndDefaultsToOff(string? configured, bool expected)
    {
        using var provider = BuildProvider(Configuration(enabled: configured));

        Assert.Equal(expected, provider.GetRequiredService<IOptions<LoadTestingOptions>>().Value.Enabled);
    }

    [Theory]
    [InlineData("2027-01-31", 2027, 1, 31)]
    [InlineData(null, null, null, null)]
    public void AddPtlLoadTesting_BindsTheExpiryDate(string? configured, int? year, int? month, int? day)
    {
        using var provider = BuildProvider(Configuration(expiresOn: configured));

        var expected = year is null ? (DateOnly?)null : new DateOnly(year.Value, month!.Value, day!.Value);
        Assert.Equal(expected, provider.GetRequiredService<IOptions<LoadTestingOptions>>().Value.ExpiresOn);
    }

    [Fact]
    public void AddPtlLoadTesting_KeepsAnAlreadyRegisteredTimeProvider()
    {
        using var provider = BuildProvider(Configuration(), timeProvider: FixedTimeProvider.DayAfterExpiry);

        Assert.Same(FixedTimeProvider.DayAfterExpiry, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public async Task StartupWarning_WhenEnabledButExpired_LogsThatTheSignInIsUnavailable()
    {
        var logs = new CapturingLoggerProvider();
        using var provider = BuildProvider(Configuration(enabled: "true"), logs, FixedTimeProvider.DayAfterExpiry);
        var hostedService = Assert.Single(provider.GetServices<IHostedService>());

        await hostedService.StartAsync(CancellationToken.None);

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("unavailable", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartupWarning_WhenEnabled_LogsWhichSignInIsBypassed()
    {
        var logs = new CapturingLoggerProvider();
        using var provider = BuildProvider(Configuration(enabled: "true"), logs);
        var hostedService = Assert.Single(provider.GetServices<IHostedService>());

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal($"LoadTesting is enabled until 2027-01-31: {BypassedSignIn} at /perf/login", entry.Message);
    }

    [Fact]
    public async Task StartupWarning_WhenDisabled_LogsNothing()
    {
        var logs = new CapturingLoggerProvider();
        using var provider = BuildProvider(Configuration(enabled: "false"), logs);
        var hostedService = Assert.Single(provider.GetServices<IHostedService>());

        await hostedService.StartAsync(CancellationToken.None);

        Assert.Empty(logs.Entries);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, List<(LogLevel Level, string Message)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (category.Contains("LoadTesting", StringComparison.Ordinal))
                {
                    entries.Add((logLevel, formatter(state, exception)));
                }
            }
        }
    }
}
