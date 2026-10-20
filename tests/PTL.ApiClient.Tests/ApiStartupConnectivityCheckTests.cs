using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PTL.ApiClient.Tests;

public class ApiStartupConnectivityCheckTests
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Api:BaseUrl"] = "http://ptlapi:8080" })
        .Build();

    [Fact]
    public async Task StartAsync_ApiHealthy_LogsInformation()
    {
        var logger = new CapturingLogger();
        var check = new ApiStartupConnectivityCheck(
            new FakeApiClient(new ApiHealthResponse("Healthy", 1, DateTime.UtcNow)), Configuration, logger);

        await RunAsync(check);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("http://ptlapi:8080", entry.Message);
    }

    [Fact]
    public async Task StartAsync_ApiReportsNonHealthy_LogsWarning()
    {
        var logger = new CapturingLogger();
        var check = new ApiStartupConnectivityCheck(
            new FakeApiClient(new ApiHealthResponse("Unhealthy", 1, DateTime.UtcNow)), Configuration, logger);

        await RunAsync(check);

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task StartAsync_ApiReturnsNull_LogsWarning()
    {
        var logger = new CapturingLogger();
        var check = new ApiStartupConnectivityCheck(new FakeApiClient(response: null), Configuration, logger);

        await RunAsync(check);

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task StartAsync_ApiUnreachable_LogsErrorWithExceptionAndDoesNotThrow()
    {
        var logger = new CapturingLogger();
        var exception = new TimeoutException("timed out");
        var check = new ApiStartupConnectivityCheck(new FakeApiClient(throwOnGetHealth: exception), Configuration, logger);

        await RunAsync(check);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Contains("http://ptlapi:8080", entry.Message);
    }

    [Fact]
    public async Task StartAsync_CancelledDuringShutdown_LogsNothing()
    {
        var logger = new CapturingLogger();
        var check = new ApiStartupConnectivityCheck(new BlockingApiClient(), Configuration, logger);

        await check.StartAsync(CancellationToken.None);
        await check.StopAsync(CancellationToken.None);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void AddPtlApiClient_RegistersStartupConnectivityCheckAsHostedService()
    {
        var services = new ServiceCollection();

        services.AddPtlApiClient(Configuration);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ApiStartupConnectivityCheck));
    }

    private static async Task RunAsync(ApiStartupConnectivityCheck check)
    {
        await check.StartAsync(CancellationToken.None);
        await check.ExecuteTask!;
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLogger : ILogger<ApiStartupConnectivityCheck>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed class BlockingApiClient : IApiClient
    {
        public async Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return null;
        }
    }

    private sealed class FakeApiClient(ApiHealthResponse? response = null, Exception? throwOnGetHealth = null) : IApiClient
    {
        public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
            throwOnGetHealth is not null
                ? Task.FromException<ApiHealthResponse?>(throwOnGetHealth)
                : Task.FromResult(response);
    }
}
