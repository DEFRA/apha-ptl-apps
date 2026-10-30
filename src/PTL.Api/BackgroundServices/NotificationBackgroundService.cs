using Microsoft.Extensions.Options;
using PTL.Core.Contract.ImportPermit;
using PTL.Core.Distribution;
using PTL.Core.Notifications;

namespace PTL.Api.BackgroundServices;

// The scheduler for the time-based emails, hosted in PTL.Api (single instance): after the start-up delay it runs the Test Consultant
// and Import Permit reminder jobs on every poll, as the legacy service ran every job on every timer tick. Every other email is sent
// when its decision is saved and is not retried.
public sealed partial class NotificationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationSchedulerOptions> schedulerOptions,
    IOptions<TabulationEmailOptions> tabulationOptions,
    IOptions<ImportPermitReminderOptions> importPermitOptions,
    TimeProvider timeProvider,
    ILogger<NotificationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!tabulationOptions.Value.Enabled && !importPermitOptions.Value.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        var settings = schedulerOptions.Value;
        var pollInterval = settings.PollInterval > TimeSpan.Zero ? settings.PollInterval : new NotificationSchedulerOptions().PollInterval;
        LogStarted(logger, settings.StartupDelay, pollInterval);

        try
        {
            await Task.Delay(settings.StartupDelay, timeProvider, stoppingToken);

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

        if (tabulationOptions.Value.Enabled)
        {
            await RunJobAsync(
                "TestConsultantEmails",
                () => scope.ServiceProvider.GetRequiredService<ITabulationEmailService>().ProcessScheduledEmailsAsync(cancellationToken),
                cancellationToken);
        }

        // Legacy ran the import permit job last, after the Test Consultant job, on the same tick.
        if (importPermitOptions.Value.Enabled)
        {
            await RunJobAsync(
                "ImportPermitReminders",
                () => scope.ServiceProvider.GetRequiredService<IImportPermitReminderService>().ProcessRemindersAsync(cancellationToken),
                cancellationToken);
        }
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

    [LoggerMessage(EventId = 41, Level = LogLevel.Information, Message = "Notification scheduler is disabled (every scheduled email job is disabled).")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 42, Level = LogLevel.Information, Message = "Notification scheduler started. StartupDelay={StartupDelay} PollInterval={PollInterval}")]
    private static partial void LogStarted(ILogger logger, TimeSpan startupDelay, TimeSpan pollInterval);

    [LoggerMessage(EventId = 43, Level = LogLevel.Error, Message = "Notification scheduler job failed and will run again on the next poll. Job={Job}")]
    private static partial void LogJobFailed(ILogger logger, string job, Exception exception);
}
