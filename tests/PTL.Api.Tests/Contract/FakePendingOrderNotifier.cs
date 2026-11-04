using PTL.Core.Contract.PendingOrder;

namespace PTL.Api.Tests.Contract;

// Records the orders PendingOrderService asked to send, without any Notify or database work.
internal sealed class FakePendingOrderNotifier : IPendingOrderNotifier
{
    public List<Guid> SentForOrders { get; } = [];

    public Task SendForOrderAsync(Guid pendingContractId, CancellationToken cancellationToken = default)
    {
        SentForOrders.Add(pendingContractId);
        return Task.CompletedTask;
    }
}
