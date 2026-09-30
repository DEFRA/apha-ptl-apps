using PTL.Core.Contract.PendingOrder;

namespace PTL.Api.Tests.Contract;

// In-memory IPendingOrderRepository test double so PendingOrderService can be tested without the
// pending-contract stored procedures.
internal sealed class FakePendingOrderRepository : IPendingOrderRepository
{
    private readonly List<PendingOrderSummaryEntity> _summaries = [];
    private readonly Dictionary<Guid, PendingOrderEntity> _orders = [];
    private readonly Dictionary<Guid, List<PendingOrderSchemeEntity>> _schemes = [];

    public List<Guid> DeletedSchemeIds { get; } = [];
    public string? LastPurchaseOrderNumber { get; private set; }

    public void Seed(PendingOrderEntity order, PendingOrderSummaryEntity summary, params PendingOrderSchemeEntity[] schemes)
    {
        _orders[order.PendingContractId] = order;
        _summaries.Add(summary);
        _schemes[order.PendingContractId] = [.. schemes];
    }

    public Task<IReadOnlyList<PendingOrderSummaryEntity>> GetSummariesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PendingOrderSummaryEntity>>(_summaries);

    public Task<PendingOrderEntity?> GetByIdAsync(Guid pendingContractId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_orders.GetValueOrDefault(pendingContractId));

    public Task<IReadOnlyList<PendingOrderSchemeEntity>> GetSchemesAsync(Guid pendingContractId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PendingOrderSchemeEntity>>(_schemes.GetValueOrDefault(pendingContractId, []));

    public Task<PendingOrderSchemeEntity?> GetSchemeByIdAsync(Guid pendingParticipantSchemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_schemes.Values.SelectMany(s => s).FirstOrDefault(s => s.PendingParticipantSchemeId == pendingParticipantSchemeId));

    public Task UpdateSchemeAsync(PendingOrderSchemeEntity scheme, bool dataConsentDeclarationGiven, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeleteSchemeAsync(Guid pendingParticipantSchemeId, CancellationToken cancellationToken = default)
    {
        DeletedSchemeIds.Add(pendingParticipantSchemeId);
        return Task.CompletedTask;
    }

    public Task UpdatePurchaseOrderNumberAsync(Guid pendingContractId, string purchaseOrderNumber, CancellationToken cancellationToken = default)
    {
        LastPurchaseOrderNumber = purchaseOrderNumber;
        return Task.CompletedTask;
    }

    public Task<bool> MarkDecidedAsync(Guid customerId, Guid pendingContractId, CancellationToken cancellationToken = default)
    {
        if (!_orders.TryGetValue(pendingContractId, out var order))
        {
            return Task.FromResult(false);
        }

        order.IsDeleted = true;
        _summaries.RemoveAll(s => s.PendingContractId == pendingContractId);
        return Task.FromResult(true);
    }
}
