using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PTL.Api.BackgroundServices;
using PTL.Api.Tests.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Notifications;

// The worker behind the Authorise click: each published tabulation handed to it is sent in turn, in a scope of its own, and a
// failing send never stops it.
public class PublicationEmailDispatcherTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static readonly Guid Published1 = Guid.Parse("d4000000-0000-0000-0000-000000000001");
    private static readonly Guid Published2 = Guid.Parse("d4000000-0000-0000-0000-000000000002");

    private sealed class ScopeProbe
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class Calls
    {
        private readonly object _gate = new();
        private readonly List<(Guid PublishedTabulationId, Guid Scope, CancellationToken Token)> _started = [];

        public IReadOnlyList<(Guid PublishedTabulationId, Guid Scope, CancellationToken Token)> Started
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
        public Channel<Guid> Finished { get; } = Channel.CreateUnbounded<Guid>();

        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Func<Guid, CancellationToken, Task>? Behaviour { get; set; }

        public void Start(Guid publishedTabulationId, Guid scope, CancellationToken token)
        {
            lock (_gate)
            {
                _started.Add((publishedTabulationId, scope, token));
            }

            FirstStarted.TrySetResult();
        }
    }

    private sealed class Notifier(Calls calls, ScopeProbe probe) : ITabulationPublishedNotifier
    {
        public async Task<PublicationNotificationRunSummary> SendForPublishedTabulationAsync(Guid publishedTabulationId, CancellationToken cancellationToken = default)
        {
            calls.Start(publishedTabulationId, probe.Id, cancellationToken);
            try
            {
                if (calls.Behaviour is { } behaviour)
                {
                    await behaviour(publishedTabulationId, cancellationToken);
                }

                return PublicationNotificationRunSummary.Empty;
            }
            finally
            {
                calls.Finished.Writer.TryWrite(publishedTabulationId);
            }
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public Calls Calls { get; } = new();
        public MessageCapturingLogger<PublicationEmailDispatcher> Logger { get; } = new();
        public PublicationEmailDispatcher Dispatcher { get; }

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddSingleton(Calls);
            services.AddScoped<ScopeProbe>();
            services.AddScoped<ITabulationPublishedNotifier, Notifier>();
            _provider = services.BuildServiceProvider();

            Dispatcher = new PublicationEmailDispatcher(_provider.GetRequiredService<IServiceScopeFactory>(), Logger);
        }

        public void Dispose()
        {
            Dispatcher.Dispose();
            _provider.Dispose();
        }
    }

    private static async Task WaitForFinishedAsync(Harness h, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await h.Calls.Finished.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task Dispatch_SendsEachPublishedTabulationInAScopeOfItsOwn()
    {
        using var h = new Harness();
        await h.Dispatcher.StartAsync(CancellationToken.None);

        h.Dispatcher.Dispatch(Published1);
        await WaitForFinishedAsync(h, 1);
        h.Dispatcher.Dispatch(Published2);
        await WaitForFinishedAsync(h, 1);
        await h.Dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal([Published1, Published2], h.Calls.Started.Select(s => s.PublishedTabulationId));
        Assert.Equal(2, h.Calls.Started.Select(s => s.Scope).Distinct().Count());
        Assert.Empty(h.Logger.Entries);

        // Stopping while it waits for the next tabulation ends it cleanly.
        Assert.True(h.Dispatcher.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Dispatch_BeforeTheWorkerHasStarted_IsSentOnceItHasAndInTheOrderGiven()
    {
        using var h = new Harness();
        h.Dispatcher.Dispatch(Published1);
        h.Dispatcher.Dispatch(Published2);

        await h.Dispatcher.StartAsync(CancellationToken.None);
        await WaitForFinishedAsync(h, 2);
        await h.Dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal([Published1, Published2], h.Calls.Started.Select(s => s.PublishedTabulationId));
    }

    [Fact]
    public async Task Dispatch_ReturnsWhileTheSendIsStillRunning_AndTheNextTabulationWaitsForIt()
    {
        using var h = new Harness();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Calls.Behaviour = (_, token) => release.Task.WaitAsync(token);
        await h.Dispatcher.StartAsync(CancellationToken.None);

        await Task.Run(() =>
        {
            h.Dispatcher.Dispatch(Published1);
            h.Dispatcher.Dispatch(Published2);
        }).WaitAsync(Timeout);
        await h.Calls.FirstStarted.Task.WaitAsync(Timeout);
        await Task.Delay(100);

        // The first send has not ended and the second has not begun.
        Assert.Equal([Published1], h.Calls.Started.Select(s => s.PublishedTabulationId));
        Assert.False(h.Calls.Finished.Reader.TryRead(out _));

        release.SetResult();
        await WaitForFinishedAsync(h, 2);
        await h.Dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal([Published1, Published2], h.Calls.Started.Select(s => s.PublishedTabulationId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispatch_SendFails_IsLoggedAndTheNextTabulationIsStillSent(bool timesOut)
    {
        using var h = new Harness();
        Exception failure = timesOut ? new TaskCanceledException("http timeout") : new InvalidOperationException("notify down");
        h.Calls.Behaviour = (id, _) => id == Published1 ? Task.FromException(failure) : Task.CompletedTask;
        await h.Dispatcher.StartAsync(CancellationToken.None);

        h.Dispatcher.Dispatch(Published1);
        h.Dispatcher.Dispatch(Published2);
        await WaitForFinishedAsync(h, 2);
        await h.Dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal([Published1, Published2], h.Calls.Started.Select(s => s.PublishedTabulationId));
        var error = Assert.Single(h.Logger.At(LogLevel.Error));
        Assert.Same(failure, error.Exception);
        Assert.Contains(error.State, kv => kv.Key == "PublishedTabulationId" && Equals(kv.Value, Published1));
        Assert.True(h.Dispatcher.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task StopAsync_WhileASendIsRunning_CancelsItAndEndsCleanlyWithoutLoggingAnError()
    {
        using var h = new Harness();
        h.Calls.Behaviour = (_, token) => Task.Delay(Timeout, token);
        await h.Dispatcher.StartAsync(CancellationToken.None);
        h.Dispatcher.Dispatch(Published1);
        await h.Calls.FirstStarted.Task.WaitAsync(Timeout);

        await h.Dispatcher.StopAsync(CancellationToken.None);

        Assert.True(h.Dispatcher.ExecuteTask!.IsCompletedSuccessfully);
        Assert.True(Assert.Single(h.Calls.Started).Token.IsCancellationRequested);
        Assert.Empty(h.Logger.At(LogLevel.Error));
    }
}
