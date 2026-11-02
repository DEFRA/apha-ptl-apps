using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PTL.Api.BackgroundServices;
using PTL.Api.Tests.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Notifications;

// The worker behind the packaging Save: each save's emails handed to it are sent in turn, in a scope of its own, and a failing
// send never stops it.
public class DistributionPostedEmailDispatcherTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static readonly IReadOnlyList<DistributionPostedEmailEntity> Batch1 = Batch("primary@example.test", "secondary@example.test");
    private static readonly IReadOnlyList<DistributionPostedEmailEntity> Batch2 = Batch("lab@example.test");

    private static IReadOnlyList<DistributionPostedEmailEntity> Batch(params string[] addresses) =>
        [.. addresses.Select(address => new DistributionPostedEmailEntity { DistributionPostedEmailId = Guid.NewGuid(), Email = address })];

    private sealed class ScopeProbe
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class Calls
    {
        private readonly object _gate = new();
        private readonly List<(IReadOnlyList<DistributionPostedEmailEntity> Emails, Guid Scope, CancellationToken Token)> _started = [];

        public IReadOnlyList<(IReadOnlyList<DistributionPostedEmailEntity> Emails, Guid Scope, CancellationToken Token)> Started
        {
            get
            {
                lock (_gate)
                {
                    return [.. _started];
                }
            }
        }

        // Written once a send has ended, whether it worked or not.
        public Channel<IReadOnlyList<DistributionPostedEmailEntity>> Finished { get; } = Channel.CreateUnbounded<IReadOnlyList<DistributionPostedEmailEntity>>();

        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Func<IReadOnlyList<DistributionPostedEmailEntity>, CancellationToken, Task>? Behaviour { get; set; }

        public void Start(IReadOnlyList<DistributionPostedEmailEntity> emails, Guid scope, CancellationToken token)
        {
            lock (_gate)
            {
                _started.Add((emails, scope, token));
            }

            FirstStarted.TrySetResult();
        }
    }

    private sealed class Notifier(Calls calls, ScopeProbe probe) : IDistributionPostedNotifier
    {
        public Task<IReadOnlyList<DistributionPostedEmailEntity>> BuildEmailsAsync(
            IReadOnlyList<PostedParticipant> posted, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<DistributionPostedRunSummary> SendAsync(
            IReadOnlyList<DistributionPostedEmailEntity> emails, CancellationToken cancellationToken = default)
        {
            calls.Start(emails, probe.Id, cancellationToken);
            try
            {
                if (calls.Behaviour is { } behaviour)
                {
                    await behaviour(emails, cancellationToken);
                }

                return DistributionPostedRunSummary.Empty;
            }
            finally
            {
                calls.Finished.Writer.TryWrite(emails);
            }
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public Calls Calls { get; } = new();
        public MessageCapturingLogger<DistributionPostedEmailDispatcher> Logger { get; } = new();
        public DistributionPostedEmailDispatcher Dispatcher { get; }

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddSingleton(Calls);
            services.AddScoped<ScopeProbe>();
            services.AddScoped<IDistributionPostedNotifier, Notifier>();
            _provider = services.BuildServiceProvider();

            Dispatcher = new DistributionPostedEmailDispatcher(_provider.GetRequiredService<IServiceScopeFactory>(), Logger);
        }

        public void Dispose()
        {
            Dispatcher.Dispose();
            _provider.Dispose();
        }
    }

    private static Channel<IReadOnlyList<DistributionPostedEmailEntity>> QueueOf(DistributionPostedEmailDispatcher dispatcher) =>
        (Channel<IReadOnlyList<DistributionPostedEmailEntity>>)typeof(DistributionPostedEmailDispatcher)
            .GetField("queue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(dispatcher)!;

    private static async Task WaitForFinishedAsync(Harness h, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await h.Calls.Finished.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task Dispatch_SendsEachBatchInAScopeOfItsOwn()
    {
        using var h = new Harness();
        await h.Dispatcher.StartAsync(CancellationToken.None);

        h.Dispatcher.Dispatch(Batch1);
        await WaitForFinishedAsync(h, 1);
        h.Dispatcher.Dispatch(Batch2);
        await WaitForFinishedAsync(h, 1);
        await h.Dispatcher.StopAsync(CancellationToken.None);

        Assert.Collection(
            h.Calls.Started,
            started => Assert.Same(Batch1, started.Emails),
            started => Assert.Same(Batch2, started.Emails));
        Assert.Equal(2, h.Calls.Started.Select(s => s.Scope).Distinct().Count());
        Assert.Empty(h.Logger.Entries);

        // Stopping while it waits for the next batch ends it cleanly.
        Assert.True(h.Dispatcher.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_TheQueueIsCompleted_SendsWhatWasQueuedThenEndsWithoutBeingCancelled()
    {
        using var h = new Harness();
        h.Dispatcher.Dispatch(Batch1);

        // Nothing in production completes the queue, so the test closes it to show the worker ends normally rather than only on shutdown.
        QueueOf(h.Dispatcher).Writer.Complete();

        await h.Dispatcher.StartAsync(CancellationToken.None);
        await h.Dispatcher.ExecuteTask!.WaitAsync(Timeout);

        Assert.Same(Batch1, Assert.Single(h.Calls.Started).Emails);
        Assert.True(h.Dispatcher.ExecuteTask.IsCompletedSuccessfully);
        Assert.Empty(h.Logger.Entries);
    }

    [Fact]
    public async Task Dispatch_BeforeTheWorkerHasStarted_IsSentOnceItHasAndInTheOrderGiven()
    {
        using var h = new Harness();
        h.Dispatcher.Dispatch(Batch1);
        h.Dispatcher.Dispatch(Batch2);

        await h.Dispatcher.StartAsync(CancellationToken.None);
        await WaitForFinishedAsync(h, 2);
        await h.Dispatcher.StopAsync(CancellationToken.None);

        Assert.Collection(
            h.Calls.Started,
            started => Assert.Same(Batch1, started.Emails),
            started => Assert.Same(Batch2, started.Emails));
    }

    [Fact]
    public async Task Dispatch_ReturnsWhileTheSendIsStillRunning_AndTheNextBatchWaitsForIt()
    {
        using var h = new Harness();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Calls.Behaviour = (_, token) => release.Task.WaitAsync(token);
        await h.Dispatcher.StartAsync(CancellationToken.None);

        // Never completes if Dispatch waits for the send, which is held until released below.
        await Task.Run(() =>
        {
            h.Dispatcher.Dispatch(Batch1);
            h.Dispatcher.Dispatch(Batch2);
        }).WaitAsync(Timeout);
        await h.Calls.FirstStarted.Task.WaitAsync(Timeout);

        // The first send has not ended and the second batch is still at the head of the queue, not begun.
        Assert.Same(Batch1, Assert.Single(h.Calls.Started).Emails);
        Assert.False(h.Calls.Finished.Reader.TryRead(out _));
        Assert.True(QueueOf(h.Dispatcher).Reader.TryPeek(out var waiting));
        Assert.Same(Batch2, waiting);

        release.SetResult();
        await WaitForFinishedAsync(h, 2);
        await h.Dispatcher.StopAsync(CancellationToken.None);

        Assert.Collection(
            h.Calls.Started,
            started => Assert.Same(Batch1, started.Emails),
            started => Assert.Same(Batch2, started.Emails));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispatch_SendFails_IsLoggedAndTheNextBatchIsStillSent(bool timesOut)
    {
        using var h = new Harness();
        Exception failure = timesOut ? new TaskCanceledException("http timeout") : new InvalidOperationException("notify down");
        h.Calls.Behaviour = (emails, _) => ReferenceEquals(emails, Batch1) ? Task.FromException(failure) : Task.CompletedTask;
        await h.Dispatcher.StartAsync(CancellationToken.None);

        h.Dispatcher.Dispatch(Batch1);
        h.Dispatcher.Dispatch(Batch2);
        await WaitForFinishedAsync(h, 2);
        await h.Dispatcher.StopAsync(CancellationToken.None);

        Assert.Collection(
            h.Calls.Started,
            started => Assert.Same(Batch1, started.Emails),
            started => Assert.Same(Batch2, started.Emails));
        var error = Assert.Single(h.Logger.Entries);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Equal(117, error.EventId.Id);
        Assert.Equal("Distribution Posted emails could not be sent after the packaging save; the unsent ones stay unsent. Queued=2", error.Message);
        Assert.Same(failure, error.Exception);
        h.Logger.AssertNoneContain("primary@example.test", "secondary@example.test", "lab@example.test", "@");
        Assert.True(h.Dispatcher.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task StopAsync_WhileASendIsRunning_CancelsItAndEndsCleanlyWithoutLoggingAnError()
    {
        using var h = new Harness();
        h.Calls.Behaviour = (_, token) => Task.Delay(Timeout, token);
        await h.Dispatcher.StartAsync(CancellationToken.None);
        h.Dispatcher.Dispatch(Batch1);
        await h.Calls.FirstStarted.Task.WaitAsync(Timeout);

        await h.Dispatcher.StopAsync(CancellationToken.None);

        Assert.True(h.Dispatcher.ExecuteTask!.IsCompletedSuccessfully);
        Assert.True(Assert.Single(h.Calls.Started).Token.IsCancellationRequested);
        Assert.Empty(h.Logger.At(LogLevel.Error));
    }

    [Fact]
    public async Task StopAsync_WhileASendIsRunning_ARealFailureOfTheSendIsStillLogged()
    {
        using var h = new Harness();
        h.Calls.Behaviour = async (_, token) =>
        {
            try
            {
                await Task.Delay(Timeout, token);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException("notify failed while stopping");
            }
        };
        await h.Dispatcher.StartAsync(CancellationToken.None);
        h.Dispatcher.Dispatch(Batch1);
        await h.Calls.FirstStarted.Task.WaitAsync(Timeout);

        await h.Dispatcher.StopAsync(CancellationToken.None);

        var error = Assert.Single(h.Logger.At(LogLevel.Error));
        Assert.Equal("notify failed while stopping", error.Exception!.Message);
        Assert.True(h.Dispatcher.ExecuteTask!.IsCompletedSuccessfully);
    }
}
