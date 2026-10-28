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

    // spiPendingCustomerDetailsEdit (update.PendingCustomerUpdateId == Guid.Empty) or
    // spuPendingCustomerDetailsEditByCustomerID (otherwise) - legacy PendingCustomerUpdate.Save's
    // insert-or-update split. Mutates update.PendingCustomerUpdateId with the new id on insert.
    Task UpsertAsync(PendingCustomerUpdate update, CancellationToken cancellationToken = default);

    // spdPendingCustomerDetailsEditByCustomerID - soft-deletes the pending record (sets
    // fldIsDeleted=1); used on both Approve and Decline, since neither path reinstates the row.
    // Returns false when no matching row was found.
    Task<bool> MarkDecidedAsync(Guid customerId, Guid pendingCustomerUpdateId, CancellationToken cancellationToken = default);

    // spiUpdatedCustomerDetailsEmail - records one accepted/declined email; it stays unsent until CustomerUpdateNotifier sends it.
    Task QueueNotificationAsync(CustomerUpdateNotification notification, CancellationToken cancellationToken = default);

    // spgaUpdatedCustomerDetailsEmail - every unsent row.
    Task<IReadOnlyList<CustomerUpdateEmailEntity>> GetUnsentNotificationsAsync(CancellationToken cancellationToken = default);

    // spuUpdatedCustomerDetailsEmail - stamps fldDateSent.
    Task MarkNotificationSentAsync(Guid notificationId, DateTime sentAt, CancellationToken cancellationToken = default);
}
