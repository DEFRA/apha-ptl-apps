using System.Threading.Channels;
using PTL.Core.Distribution;

namespace PTL.Api.BackgroundServices;

// Sends the Distribution Posted emails of a packaging save as soon as it has committed, one save at a time, so the packaging user never waits
// for them. Nothing polls: the worker sleeps until a save hands it its emails.
public sealed partial class DistributionPostedEmailDispatcher(IServiceScopeFactory scopeFactory, ILogger<DistributionPostedEmailDispatcher> logger)
    : BackgroundService, IDistributionPostedEmailDispatcher
{
    private readonly Channel<IReadOnlyList<DistributionPostedEmailEntity>> queue =
        Channel.CreateUnbounded<IReadOnlyList<DistributionPostedEmailEntity>>(new UnboundedChannelOptions { SingleReader = true });

    public void Dispatch(IReadOnlyList<DistributionPostedEmailEntity> emails) => queue.Writer.TryWrite(emails);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var emails in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await SendAsync(emails, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is stopping.
        }
    }

    // A failure is logged and never stops the worker; the emails that were not sent stay unsent in tblDistributionPostedEmail.
    private async Task SendAsync(IReadOnlyList<DistributionPostedEmailEntity> emails, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IDistributionPostedNotifier>().SendAsync(emails, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogSendFailed(logger, emails.Count, ex);
        }
    }

    [LoggerMessage(EventId = 117, Level = LogLevel.Error, Message = "Distribution Posted emails could not be sent after the packaging save; the unsent ones stay unsent. Queued={Queued}")]
    private static partial void LogSendFailed(ILogger logger, int queued, Exception exception);
}
