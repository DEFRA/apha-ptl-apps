using Microsoft.Extensions.Options;
using PTL.Core.Distribution;

namespace PTL.Api.BackgroundServices;

// Runs inside PTL.Api (single instance): after the start-up delay it polls for the chaser emails, the first
// emails whose immediate send failed, and the unsent publication notifications.
public sealed partial class TabulationEmailBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<TabulationEmailOptions> emailOptions,
    IOptions<PublicationNotificationOptions> publicationOptions,
    TimeProvider timeProvider,
    ILogger<TabulationEmailBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = emailOptions.Value;
        if (!options.Enabled && !publicationOptions.Value.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        var pollInterval = options.PollInterval > TimeSpan.Zero ? options.PollInterval : new TabulationEmailOptions().PollInterval;
        LogStarted(logger, options.StartupDelay, pollInterval);

        try
        {
            await Task.Delay(options.StartupDelay, timeProvider, stoppingToken);

            using var timer = new PeriodicTimer(pollInterval, timeProvider);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // The host is stopping.
        }
    }

    // One scheduler execution. A failing job is logged and never stops the host, so the next run retries it.
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        await RunJobAsync(
            "TestConsultantEmails",
            () => scope.ServiceProvider.GetRequiredService<ITabulationEmailService>().ProcessScheduledEmailsAsync(cancellationToken),
            cancellationToken);

        await RunJobAsync(
            "PublicationNotifications",
            () => scope.ServiceProvider.GetRequiredService<ITabulationPublishedNotifier>().SendUnsentAsync(cancellationToken),
            cancellationToken);
    }

    private async Task RunJobAsync(string job, Func<Task> run, CancellationToken cancellationToken)
    {
        try
        {
            await run();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogJobFailed(logger, job, ex);
        }
    }

    [LoggerMessage(EventId = 41, Level = LogLevel.Information, Message = "Tabulation email scheduler is disabled (TabulationEmails:Enabled and PublicationNotifications:Enabled are both false).")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 42, Level = LogLevel.Information, Message = "Tabulation email scheduler started. StartupDelay={StartupDelay} PollInterval={PollInterval}")]
    private static partial void LogStarted(ILogger logger, TimeSpan startupDelay, TimeSpan pollInterval);

    [LoggerMessage(EventId = 43, Level = LogLevel.Error, Message = "Tabulation email scheduler job failed and will run again on the next poll. Job={Job}")]
    private static partial void LogJobFailed(ILogger logger, string job, Exception exception);
}
