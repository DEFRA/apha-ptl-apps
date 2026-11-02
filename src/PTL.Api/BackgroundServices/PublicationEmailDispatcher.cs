using System.Threading.Channels;
using PTL.Core.Distribution;

namespace PTL.Api.BackgroundServices;

// Sends a tabulation's publication emails as soon as Authorise has committed, one tabulation at a time, so the sign-off user
// never waits for them. Nothing polls: the worker sleeps until Authorise hands it a tabulation.
public sealed partial class PublicationEmailDispatcher(IServiceScopeFactory scopeFactory, ILogger<PublicationEmailDispatcher> logger)
    : BackgroundService, IPublicationEmailDispatcher
{
    private readonly Channel<Guid> queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public void Dispatch(Guid publishedTabulationId) => queue.Writer.TryWrite(publishedTabulationId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var publishedTabulationId in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await SendAsync(publishedTabulationId, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is stopping.
        }
    }

    // A failure is logged and never stops the worker; the rows that were not sent stay unsent in tblNotificationEmail.
    private async Task SendAsync(Guid publishedTabulationId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ITabulationPublishedNotifier>()
                .SendForPublishedTabulationAsync(publishedTabulationId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogSendFailed(logger, publishedTabulationId, ex);
        }
    }

    [LoggerMessage(EventId = 25, Level = LogLevel.Error, Message = "Publication notification emails could not be sent after Authorise; the unsent ones stay unsent. PublishedTabulationId={PublishedTabulationId}")]
    private static partial void LogSendFailed(ILogger logger, Guid publishedTabulationId, Exception exception);
}
