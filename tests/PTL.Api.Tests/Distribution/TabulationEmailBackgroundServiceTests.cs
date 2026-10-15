using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Api.BackgroundServices;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// The hosted scheduler: each run resolves the two jobs from a fresh scope; a failing job never stops the host.
public class TabulationEmailBackgroundServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private sealed class ScopeProbe
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class Calls
    {
        private readonly object _gate = new();

        public List<string> Order { get; } = [];
        public List<(string Job, Guid Scope, CancellationToken Token)> Invocations { get; } = [];
        public Exception? EmailFailure { get; set; }
        public Exception? NotifierFailure { get; set; }
        public Func<CancellationToken, Task>? EmailBehaviour { get; set; }
        public TaskCompletionSource FirstEmailRun { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ThreeEmailRuns { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int EmailRuns
        {
            get
            {
                lock (_gate)
                {
                    return Order.Count(o => o == "email");
                }
            }
        }

        public void Record(string job, Guid scope, CancellationToken token)
        {
            lock (_gate)
            {
                Order.Add(job);
                Invocations.Add((job, scope, token));
                if (job == "email")
                {
                    FirstEmailRun.TrySetResult();
                    if (Order.Count(o => o == "email") >= 3)
                    {
                        ThreeEmailRuns.TrySetResult();
                    }
                }
            }
        }
    }

    private sealed class EmailJob(Calls calls, ScopeProbe probe) : ITabulationEmailService
    {
        public Task SendFirstEmailsAsync(IReadOnlyList<Guid> monthlyDistributionSchemeIds, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public async Task<TabulationEmailRunSummary> ProcessScheduledEmailsAsync(CancellationToken cancellationToken = default)
        {
            calls.Record("email", probe.Id, cancellationToken);
            if (calls.EmailBehaviour is { } behaviour)
            {
                await behaviour(cancellationToken);
            }

            return calls.EmailFailure is null ? TabulationEmailRunSummary.Empty : throw calls.EmailFailure;
        }
    }

    private sealed class NotifierJob(Calls calls, ScopeProbe probe) : ITabulationPublishedNotifier
    {
        public Task<PublicationNotificationRunSummary> SendUnsentAsync(CancellationToken cancellationToken = default)
        {
            calls.Record("notifier", probe.Id, cancellationToken);
            return calls.NotifierFailure is null
                ? Task.FromResult(PublicationNotificationRunSummary.Empty)
                : Task.FromException<PublicationNotificationRunSummary>(calls.NotifierFailure);
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public Calls Calls { get; } = new();
        public MessageCapturingLogger<TabulationEmailBackgroundService> Logger { get; } = new();
        public TabulationEmailBackgroundService Service { get; }

        public Harness(
            bool emailsEnabled = true,
            bool publicationsEnabled = true,
            TimeSpan? startupDelay = null,
            TimeSpan? pollInterval = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(Calls);
            services.AddScoped<ScopeProbe>();
            services.AddScoped<ITabulationEmailService, EmailJob>();
            services.AddScoped<ITabulationPublishedNotifier, NotifierJob>();
            _provider = services.BuildServiceProvider();

            Service = new TabulationEmailBackgroundService(
                _provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new TabulationEmailOptions
                {
                    Enabled = emailsEnabled,
                    StartupDelay = startupDelay ?? TimeSpan.FromMilliseconds(10),
                    PollInterval = pollInterval ?? TimeSpan.FromMilliseconds(10),
                }),
                Options.Create(new PublicationNotificationOptions { Enabled = publicationsEnabled }),
                TimeProvider.System,
                Logger);
        }

        public void Dispose()
        {
            Service.Dispose();
            _provider.Dispose();
        }
    }

    // ---- RunOnceAsync ----

    [Fact]
    public async Task RunOnceAsync_RunsTheEmailJobThenThePublicationJobInOneScopeWithTheToken()
    {
        using var h = new Harness();
        using var cts = new CancellationTokenSource();

        await h.Service.RunOnceAsync(cts.Token);

        Assert.Equal(["email", "notifier"], h.Calls.Order);
        Assert.Single(h.Calls.Invocations.Select(i => i.Scope).Distinct());
        Assert.All(h.Calls.Invocations, i => Assert.Equal(cts.Token, i.Token));
        Assert.Empty(h.Logger.Entries);
    }

    [Fact]
    public async Task RunOnceAsync_EachRunUsesAFreshScope()
    {
        using var h = new Harness();

        await h.Service.RunOnceAsync(CancellationToken.None);
        await h.Service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, h.Calls.Invocations.Select(i => i.Scope).Distinct().Count());
    }

    [Fact]
    public async Task RunOnceAsync_EmailJobThrows_LogsAnErrorAndStillRunsThePublicationJob()
    {
        using var h = new Harness();
        var failure = new InvalidOperationException("database down");
        h.Calls.EmailFailure = failure;

        await h.Service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(["email", "notifier"], h.Calls.Order);
        var error = Assert.Single(h.Logger.At(LogLevel.Error));
        Assert.Same(failure, error.Exception);
        Assert.Contains(error.State, kv => kv.Key == "Job" && Equals(kv.Value, "TestConsultantEmails"));
    }

    [Fact]
    public async Task RunOnceAsync_PublicationJobThrows_LogsAnErrorWithoutThrowing()
    {
        using var h = new Harness();
        var failure = new InvalidOperationException("notify down");
        h.Calls.NotifierFailure = failure;

        await h.Service.RunOnceAsync(CancellationToken.None);

        var error = Assert.Single(h.Logger.At(LogLevel.Error));
        Assert.Same(failure, error.Exception);
        Assert.Contains(error.State, kv => kv.Key == "Job" && Equals(kv.Value, "PublicationNotifications"));
    }

    [Fact]
    public async Task RunOnceAsync_BothJobsThrow_LogsBoth()
    {
        using var h = new Harness();
        h.Calls.EmailFailure = new InvalidOperationException("one");
        h.Calls.NotifierFailure = new InvalidOperationException("two");

        await h.Service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, h.Logger.At(LogLevel.Error).Count());
    }

    [Fact]
    public async Task RunOnceAsync_JobTimesOutWithoutShutdown_IsLoggedAndTheOtherJobStillRuns()
    {
        using var h = new Harness();
        h.Calls.EmailFailure = new TaskCanceledException("http timeout");

        await h.Service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(["email", "notifier"], h.Calls.Order);
        Assert.Single(h.Logger.At(LogLevel.Error));
    }

    [Fact]
    public async Task RunOnceAsync_CancelledWhileTheTokenIsRequested_Propagates()
    {
        using var h = new Harness();
        using var cts = new CancellationTokenSource();
        h.Calls.EmailBehaviour = async token =>
        {
            await cts.CancelAsync();
            token.ThrowIfCancellationRequested();
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Service.RunOnceAsync(cts.Token));

        Assert.Equal(["email"], h.Calls.Order);
        Assert.Empty(h.Logger.At(LogLevel.Error));
    }

    // ---- ExecuteAsync ----

    [Fact]
    public async Task ExecuteAsync_BothSchedulersDisabled_ReturnsImmediatelyWithoutRunningJobs()
    {
        using var h = new Harness(emailsEnabled: false, publicationsEnabled: false, startupDelay: TimeSpan.Zero);

        await h.Service.StartAsync(CancellationToken.None);
        await h.Service.ExecuteTask!.WaitAsync(Timeout);
        await h.Service.StopAsync(CancellationToken.None);

        Assert.Empty(h.Calls.Order);
        var log = Assert.Single(h.Logger.Entries);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Contains("disabled", log.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ExecuteAsync_EitherSchedulerEnabled_RunsTheJobsRepeatedlyAfterTheStartupDelay(bool emails, bool publications)
    {
        using var h = new Harness(emailsEnabled: emails, publicationsEnabled: publications);

        await h.Service.StartAsync(CancellationToken.None);
        await h.Calls.ThreeEmailRuns.Task.WaitAsync(Timeout);
        await h.Service.StopAsync(CancellationToken.None);

        Assert.True(h.Calls.EmailRuns >= 3);
        Assert.Equal("email", h.Calls.Order[0]);
        Assert.Equal("notifier", h.Calls.Order[1]);
        Assert.Empty(h.Logger.At(LogLevel.Error));
        var started = Assert.Single(h.Logger.Entries, e => e.Message.StartsWith("Tabulation email scheduler started", StringComparison.Ordinal));
        Assert.Contains(started.State, kv => kv.Key == "PollInterval" && Equals(kv.Value, TimeSpan.FromMilliseconds(10)));
        Assert.Contains(started.State, kv => kv.Key == "StartupDelay" && Equals(kv.Value, TimeSpan.FromMilliseconds(10)));
    }

    [Fact]
    public async Task ExecuteAsync_WaitsForTheStartupDelayBeforeTheFirstRun()
    {
        using var h = new Harness(startupDelay: TimeSpan.FromHours(1));

        await h.Service.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        Assert.Empty(h.Calls.Order);
        Assert.False(h.Service.ExecuteTask!.IsCompleted);

        await h.Service.StopAsync(CancellationToken.None);

        Assert.True(h.Service.ExecuteTask.IsCompletedSuccessfully);
        Assert.Empty(h.Calls.Order);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ExecuteAsync_NonPositivePollInterval_FallsBackToFiveMinutes(int milliseconds)
    {
        using var h = new Harness(pollInterval: TimeSpan.FromMilliseconds(milliseconds));

        await h.Service.StartAsync(CancellationToken.None);
        await h.Calls.FirstEmailRun.Task.WaitAsync(Timeout);
        await h.Service.StopAsync(CancellationToken.None);

        var started = Assert.Single(h.Logger.Entries, e => e.Message.StartsWith("Tabulation email scheduler started", StringComparison.Ordinal));
        Assert.Contains(started.State, kv => kv.Key == "PollInterval" && Equals(kv.Value, TimeSpan.FromMinutes(5)));
        Assert.Equal(1, h.Calls.EmailRuns);
    }

    [Fact]
    public async Task ExecuteAsync_FailingJobs_DoNotStopThePollingLoop()
    {
        using var h = new Harness();
        h.Calls.EmailFailure = new InvalidOperationException("database down");
        h.Calls.NotifierFailure = new InvalidOperationException("notify down");

        await h.Service.StartAsync(CancellationToken.None);
        await h.Calls.ThreeEmailRuns.Task.WaitAsync(Timeout);
        await h.Service.StopAsync(CancellationToken.None);

        Assert.True(h.Logger.At(LogLevel.Error).Count() >= 6);
        Assert.True(h.Service.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_StoppedWhileAJobIsRunning_EndsCleanlyWithoutThrowing()
    {
        using var h = new Harness();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Calls.EmailBehaviour = async token =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout, token);
        };

        await h.Service.StartAsync(CancellationToken.None);
        await started.Task.WaitAsync(Timeout);
        await h.Service.StopAsync(CancellationToken.None);

        Assert.True(h.Service.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Equal(["email"], h.Calls.Order);
        Assert.Empty(h.Logger.At(LogLevel.Error));
    }

    [Fact]
    public async Task ExecuteAsync_AfterStop_NoFurtherRunsHappen()
    {
        using var h = new Harness();

        await h.Service.StartAsync(CancellationToken.None);
        await h.Calls.ThreeEmailRuns.Task.WaitAsync(Timeout);
        await h.Service.StopAsync(CancellationToken.None);
        var runs = h.Calls.EmailRuns;
        await Task.Delay(100);

        Assert.Equal(runs, h.Calls.EmailRuns);
    }
}
