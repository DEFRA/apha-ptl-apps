using Microsoft.Extensions.Options;
using PTL.Core.Contract.PendingOrder;
using PTL.Core.Customer;
using PTL.Core.Distribution;
using PTL.Core.Notifications;
using PTL.Core.Participant;

namespace PTL.Api.BackgroundServices;

// The one scheduler for every email job, hosted in PTL.Api (single instance): after the start-up delay it runs each job
// on every poll - the Test Consultant emails, the publication notifications, and the online contract order, customer
// update and participant update emails that failed to send when the decision was made. Each job checks its own Enabled switch.
public sealed partial class NotificationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationSchedulerOptions> schedulerOptions,
    IOptions<TabulationEmailOptions> tabulationOptions,
    IOptions<PublicationNotificationOptions> publicationOptions,
    IOptions<OnlineOrderEmailOptions> onlineOrderOptions,
    IOptions<CustomerUpdateEmailOptions> customerUpdateOptions,
    IOptions<ParticipantUpdateEmailOptions> participantUpdateOptions,
    TimeProvider timeProvider,
    ILogger<NotificationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!tabulationOptions.Value.Enabled
            && !publicationOptions.Value.Enabled
            && !onlineOrderOptions.Value.Enabled
            && !customerUpdateOptions.Value.Enabled
            && !participantUpdateOptions.Value.Enabled)
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

    // One scheduler execution. A failing job is logged and never stops the host or the other jobs, so the next run retries it.
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

        await RunJobAsync(
            "OnlineOrderEmails",
            () => scope.ServiceProvider.GetRequiredService<IPendingOrderNotifier>().SendUnsentAsync(cancellationToken),
            cancellationToken);

        await RunJobAsync(
            "CustomerUpdateEmails",
            () => scope.ServiceProvider.GetRequiredService<ICustomerUpdateNotifier>().SendUnsentAsync(cancellationToken),
            cancellationToken);

        await RunJobAsync(
            "ParticipantUpdateEmails",
            () => scope.ServiceProvider.GetRequiredService<IParticipantUpdateNotifier>().SendUnsentAsync(cancellationToken),
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

    [LoggerMessage(EventId = 41, Level = LogLevel.Information, Message = "Notification scheduler is disabled (every email job is disabled).")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 42, Level = LogLevel.Information, Message = "Notification scheduler started. StartupDelay={StartupDelay} PollInterval={PollInterval}")]
    private static partial void LogStarted(ILogger logger, TimeSpan startupDelay, TimeSpan pollInterval);

    [LoggerMessage(EventId = 43, Level = LogLevel.Error, Message = "Notification scheduler job failed and will run again on the next poll. Job={Job}")]
    private static partial void LogJobFailed(ILogger logger, string job, Exception exception);
}
