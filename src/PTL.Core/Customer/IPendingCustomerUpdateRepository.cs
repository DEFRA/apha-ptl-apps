namespace PTL.Core.Customer;

// Defined in Core (not Data) so CustomerService can depend on the abstraction without Core
// referencing Data; PTL.Data.Customer.PendingCustomerUpdateRepository implements this.
public interface IPendingCustomerUpdateRepository
{
    // spgaPendingCustomerDetailsEditInfo - every outstanding (submitted, not-deleted) pending update.
    Task<IReadOnlyList<PendingCustomerUpdateSummaryEntity>> GetSummariesAsync(CancellationToken cancellationToken = default);

    // spgPendingCustomerDetailsEditByCustomerID @IsSubmitted=1 - the single outstanding pending
    // update for a customer, or null if there is none.
    Task<PendingCustomerUpdate?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    // spdPendingCustomerDetailsEditByCustomerID - soft-deletes the pending record (sets
    // fldIsDeleted=1); used on both Approve and Decline, since neither path reinstates the row.
    // Returns false when no matching row was found.
    Task<bool> MarkDecidedAsync(Guid customerId, Guid pendingCustomerUpdateId, CancellationToken cancellationToken = default);
}
