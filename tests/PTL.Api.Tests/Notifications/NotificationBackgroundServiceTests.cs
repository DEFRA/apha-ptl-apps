using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PTL.Api.BackgroundServices;
using PTL.Api.Tests.Distribution;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Customer;
using PTL.Core.Distribution;
using PTL.Core.Notifications;
using PTL.Core.Participant;

namespace PTL.Api.Tests.Notifications;

// The one hosted scheduler: each run resolves the four email jobs from a fresh scope; a failing job never stops the
// host or the other jobs.
public class NotificationBackgroundServiceTests
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
        public Exception? OnlineOrderFailure { get; set; }
        public Exception? CustomerUpdateFailure { get; set; }
        public Exception? ParticipantUpdateFailure { get; set; }
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

    private sealed class OnlineOrderJob(Calls calls, ScopeProbe probe) : IPendingOrderNotifier
    {
        public Task SendForOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<OnlineOrderEmailRunSummary> SendUnsentAsync(CancellationToken cancellationToken = default)
        {
            calls.Record("onlineOrder", probe.Id, cancellationToken);
            return calls.OnlineOrderFailure is null
                ? Task.FromResult(OnlineOrderEmailRunSummary.Empty)
                : Task.FromException<OnlineOrderEmailRunSummary>(calls.OnlineOrderFailure);
        }
    }

    private sealed class CustomerUpdateJob(Calls calls, ScopeProbe probe) : ICustomerUpdateNotifier
    {
        public Task SendForUpdateAsync(Guid pendingCustomerUpdateId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendUnsentAsync(CancellationToken cancellationToken = default)
        {
            calls.Record("customerUpdate", probe.Id, cancellationToken);
            return calls.CustomerUpdateFailure is null ? Task.CompletedTask : Task.FromException(calls.CustomerUpdateFailure);
        }
    }

    private sealed class ParticipantUpdateJob(Calls calls, ScopeProbe probe) : IParticipantUpdateNotifier
    {
        public Task SendForUpdateAsync(Guid pendingParticipantUpdateId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendUnsentAsync(CancellationToken cancellationToken = default)
        {
            calls.Record("participantUpdate", probe.Id, cancellationToken);
            return calls.ParticipantUpdateFailure is null ? Task.CompletedTask : Task.FromException(calls.ParticipantUpdateFailure);
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public Calls Calls { get; } = new();
        public MessageCapturingLogger<NotificationBackgroundService> Logger { get; } = new();
        public NotificationBackgroundService Service { get; }

        public Harness(
            bool emailsEnabled = true,
            bool onlineOrdersEnabled = true,
            bool customerUpdatesEnabled = true,
            bool participantUpdatesEnabled = true,
            TimeSpan? startupDelay = null,
            TimeSpan? pollInterval = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(Calls);
            services.AddScoped<ScopeProbe>();
            services.AddScoped<ITabulationEmailService, EmailJob>();
            services.AddScoped<IPendingOrderNotifier, OnlineOrderJob>();
            services.AddScoped<ICustomerUpdateNotifier, CustomerUpdateJob>();
            services.AddScoped<IParticipantUpdateNotifier, ParticipantUpdateJob>();
            _provider = services.BuildServiceProvider();

            Service = new NotificationBackgroundService(
                _provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new NotificationSchedulerOptions
                {
                    StartupDelay = startupDelay ?? TimeSpan.FromMilliseconds(10),
                    PollInterval = pollInterval ?? TimeSpan.FromMilliseconds(10),
                }),
                Options.Create(new TabulationEmailOptions { Enabled = emailsEnabled }),
                Options.Create(new OnlineOrderEmailOptions { Enabled = onlineOrdersEnabled }),
                Options.Create(new CustomerUpdateEmailOptions { Enabled = customerUpdatesEnabled }),
                Options.Create(new ParticipantUpdateEmailOptions { Enabled = participantUpdatesEnabled }),
                TimeProvider.System,
                Logger);
        }

        public void Dispose()
        {
            Service.Dispose();
            _provider.Dispose();
        }
    }

    private static readonly string[] AllJobsInOrder = ["email", "onlineOrder", "customerUpdate", "participantUpdate"];

    [Fact]
    public void NotificationSchedulerOptions_Defaults_AreFiveMinutesAfterAThirtySecondDelay()
    {
        var options = new NotificationSchedulerOptions();

        Assert.Equal("NotificationScheduler", NotificationSchedulerOptions.SectionName);
        Assert.Equal(TimeSpan.FromMinutes(5), options.PollInterval);
        Assert.Equal(TimeSpan.FromSeconds(30), options.StartupDelay);
    }

    // ---- RunOnceAsync ----

    [Fact]
    public async Task RunOnceAsync_RunsAllFourJobsInOneScopeWithTheToken()
    {
        using var h = new Harness();
        using var cts = new CancellationTokenSource();

        await h.Service.RunOnceAsync(cts.Token);

        Assert.Equal(AllJobsInOrder, h.Calls.Order);
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

    [Theory]
    [InlineData("email", "TestConsultantEmails")]
    [InlineData("onlineOrder", "OnlineOrderEmails")]
    [InlineData("customerUpdate", "CustomerUpdateEmails")]
    [InlineData("participantUpdate", "ParticipantUpdateEmails")]
    public async Task RunOnceAsync_OneJobThrows_LogsItByNameAndStillRunsTheOthers(string failing, string jobName)
    {
        using var h = new Harness();
        var failure = new InvalidOperationException("down");
        switch (failing)
        {
            case "email": h.Calls.EmailFailure = failure; break;
            case "onlineOrder": h.Calls.OnlineOrderFailure = failure; break;
            case "customerUpdate": h.Calls.CustomerUpdateFailure = failure; break;
            default: h.Calls.ParticipantUpdateFailure = failure; break;
        }

        await h.Service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(AllJobsInOrder, h.Calls.Order);
        var error = Assert.Single(h.Logger.At(LogLevel.Error));
        Assert.Same(failure, error.Exception);
        Assert.Contains(error.State, kv => kv.Key == "Job" && Equals(kv.Value, jobName));
    }

    [Fact]
    public async Task RunOnceAsync_AllJobsThrow_LogsEach()
    {
        using var h = new Harness();
        h.Calls.EmailFailure = new InvalidOperationException("one");
        h.Calls.OnlineOrderFailure = new InvalidOperationException("two");
        h.Calls.CustomerUpdateFailure = new InvalidOperationException("three");
        h.Calls.ParticipantUpdateFailure = new InvalidOperationException("four");

        await h.Service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(4, h.Logger.At(LogLevel.Error).Count());
    }

    [Fact]
    public async Task RunOnceAsync_JobTimesOutWithoutShutdown_IsLoggedAndTheOthersStillRun()
    {
        using var h = new Harness();
        h.Calls.EmailFailure = new TaskCanceledException("http timeout");

        await h.Service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(AllJobsInOrder, h.Calls.Order);
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
    public async Task ExecuteAsync_EveryJobDisabled_ReturnsImmediatelyWithoutRunningJobs()
    {
        using var h = new Harness(emailsEnabled: false, onlineOrdersEnabled: false, customerUpdatesEnabled: false, participantUpdatesEnabled: false, startupDelay: TimeSpan.Zero);

        await h.Service.StartAsync(CancellationToken.None);
        await h.Service.ExecuteTask!.WaitAsync(Timeout);
        await h.Service.StopAsync(CancellationToken.None);

        Assert.Empty(h.Calls.Order);
        var log = Assert.Single(h.Logger.Entries);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Contains("disabled", log.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    [InlineData(true, true, true, true)]
    public async Task ExecuteAsync_AnyJobEnabled_RunsTheJobsRepeatedlyAfterTheStartupDelay(bool emails, bool onlineOrders, bool customerUpdates, bool participantUpdates)
    {
        using var h = new Harness(emailsEnabled: emails, onlineOrdersEnabled: onlineOrders, customerUpdatesEnabled: customerUpdates, participantUpdatesEnabled: participantUpdates);

        await h.Service.StartAsync(CancellationToken.None);
        await h.Calls.ThreeEmailRuns.Task.WaitAsync(Timeout);
        await h.Service.StopAsync(CancellationToken.None);

        Assert.True(h.Calls.EmailRuns >= 3);
        Assert.Equal(AllJobsInOrder, h.Calls.Order.Take(4));
        Assert.Empty(h.Logger.At(LogLevel.Error));
        var started = Assert.Single(h.Logger.Entries, e => e.Message.StartsWith("Notification scheduler started", StringComparison.Ordinal));
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

        var started = Assert.Single(h.Logger.Entries, e => e.Message.StartsWith("Notification scheduler started", StringComparison.Ordinal));
        Assert.Contains(started.State, kv => kv.Key == "PollInterval" && Equals(kv.Value, TimeSpan.FromMinutes(5)));
        Assert.Equal(1, h.Calls.EmailRuns);
    }

    [Fact]
    public async Task ExecuteAsync_FailingJobs_DoNotStopThePollingLoop()
    {
        using var h = new Harness();
        h.Calls.EmailFailure = new InvalidOperationException("database down");
        h.Calls.OnlineOrderFailure = new InvalidOperationException("notify down");
        h.Calls.CustomerUpdateFailure = new InvalidOperationException("notify down");
        h.Calls.ParticipantUpdateFailure = new InvalidOperationException("notify down");

        await h.Service.StartAsync(CancellationToken.None);
        await h.Calls.ThreeEmailRuns.Task.WaitAsync(Timeout);
        await h.Service.StopAsync(CancellationToken.None);

        Assert.True(h.Logger.At(LogLevel.Error).Count() >= 12);
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
