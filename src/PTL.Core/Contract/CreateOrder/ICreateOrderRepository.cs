using PTL.Core.Contract.PendingOrder;

namespace PTL.Core.Contract.CreateOrder;

// Participant-facing half of the pending-order stored procedures (legacy PendingContractOrder.aspx).
// Reading and editing the pending scheme lines reuses IPendingOrderRepository; deleting the draft
// order reuses IPendingOrderRepository.MarkDecidedAsync (spdPendingContractByPendingContractID).
public interface ICreateOrderRepository
{
    // spgPendingContractByCustomerID - the customer's unsubmitted (draft) or submitted pending order
    // for the year, excluding deleted ones. Null when none exists.
    Task<PendingOrderEntity?> GetPendingOrderAsync(Guid customerId, int yearId, bool isSubmitted, CancellationToken cancellationToken = default);

    // spiPendingContract - creates an empty, unsubmitted pending order.
    Task CreatePendingOrderAsync(Guid pendingContractId, Guid customerId, int yearId, CancellationToken cancellationToken = default);

    // spgaAvailableSchemeInfoByYearId.
    Task<IReadOnlyList<AvailableSchemeEntity>> GetAvailableSchemesAsync(int yearId, CancellationToken cancellationToken = default);

    // spgPastParticipantSchemeByParticipantSchemeId.
    Task<IReadOnlyList<ParticipatingSchemeEntity>> GetParticipatingSchemesAsync(Guid participantId, int yearId, int participatingYearId, CancellationToken cancellationToken = default);

    // spiPendingParticipantScheme - inserts one pending scheme line.
    Task InsertSchemeAsync(PendingOrderSchemeEntity scheme, bool nonUk, CancellationToken cancellationToken = default);
}
