using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

internal sealed partial class FakeDistributionRepository
{
    public List<IReadOnlyList<TabulationSendWrite>> TabulationSends { get; } = [];

    public Exception? TabulationSendError { get; set; }

    public Task SendTabulationSchemesAsync(IReadOnlyList<TabulationSendWrite> writes, CancellationToken cancellationToken = default)
    {
        if (TabulationSendError is not null)
        {
            return Task.FromException(TabulationSendError);
        }

        TabulationSends.Add(writes);
        return Task.CompletedTask;
    }
}
