namespace PTL.Core.Contract.PendingOrder;

// Defined in Core (not Data) so PendingOrderService can depend on the abstraction without Core
// referencing Data; PTL.Data.Contract.PendingOrder.PendingOrderRepository implements this.
public interface IPendingOrderRepository
{
    // spgaPendingContracts - unfiltered; the service applies legacy's in-memory year/submitted/
    // deleted filtering.
    Task<IReadOnlyList<PendingOrderSummaryEntity>> GetSummariesAsync(CancellationToken cancellationToken = default);

    // spgPendingContractByPendingContractId.
    Task<PendingOrderEntity?> GetByIdAsync(Guid pendingContractId, CancellationToken cancellationToken = default);

    // spgPendingParticipantSchemeByPendingContractId.
    Task<IReadOnlyList<PendingOrderSchemeEntity>> GetSchemesAsync(Guid pendingContractId, CancellationToken cancellationToken = default);

    // spgPendingParticipantSchemeByPendingParticipantSchemeId - the only fetch that returns the
    // fldDistributionMonthXxxIsContracted flags the grid needs; the list procedure omits them.
    Task<PendingOrderSchemeEntity?> GetSchemeByIdAsync(Guid pendingParticipantSchemeId, CancellationToken cancellationToken = default);

    // spuPendingParticipantSchemeByPendingParticipantSchemeId - the per-row month / licence /
    // removal edit legacy performs on every checkbox postback.
    Task UpdateSchemeAsync(PendingOrderSchemeEntity scheme, bool dataConsentDeclarationGiven, CancellationToken cancellationToken = default);

    // spdPendingParticipantSchemeByPendingParticipantSchemeId - a genuine DELETE, used by Decline.
    Task DeleteSchemeAsync(Guid pendingParticipantSchemeId, CancellationToken cancellationToken = default);

    // spuPendingContractByPendingContractID - persists the purchase order number before approval.
    Task UpdatePurchaseOrderNumberAsync(Guid pendingContractId, string purchaseOrderNumber, CancellationToken cancellationToken = default);

    // spdPendingContractByPendingContractID - soft-deletes (fldIsDeleted = 1). Returns false when
    // the order is still outstanding afterwards.
    Task<bool> MarkDecidedAsync(Guid customerId, Guid pendingContractId, CancellationToken cancellationToken = default);

    // spiOnlineContractOrderEmail - queues one approve/decline email for the order notification service.
    Task QueueNotificationAsync(PendingOrderNotification notification, CancellationToken cancellationToken = default);
}

// One row of tblOnlineContractOrderEmail (legacy PendingContractOrder.EmailOrderNotifications). ContractId is
// Guid.Empty for a declined order, as legacy passed Nothing.
public sealed record PendingOrderNotification(
    string Email,
    Guid ContractId,
    Guid PendingContractId,
    int YearId,
    string CustomerName,
    string Notes,
    bool IsApproved);

// Thrown when Approve/Decline input breaks a legacy rule (purchase order number 255, comments 2000).
public sealed class PendingOrderValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("Pending order validation failed.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
