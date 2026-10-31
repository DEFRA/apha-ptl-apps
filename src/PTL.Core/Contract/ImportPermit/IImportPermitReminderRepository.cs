namespace PTL.Core.Contract.ImportPermit;

// Defined in Core so ImportPermitReminderService depends on the abstraction only;
// PTL.Data.Contract.ImportPermit.ImportPermitReminderRepository implements it.
public interface IImportPermitReminderRepository
{
    // spgaContractOrderImportPermitForEmail @DaysBeforeDistribution - returns customers then
    // participants. Every eligibility rule, the 3-day catch-up window and the already-reminded
    // exclusion live in that procedure, not here.
    Task<ImportPermitReminderData> GetDueAsync(int daysBeforeDistribution, CancellationToken cancellationToken = default);

    // spiImportPermitReminderSent - appends a row to tblImportPermitReminders. A null participantId
    // records the customer-level reminder, which is how the procedure tells the two apart.
    Task MarkReminderSentAsync(
        Guid contractId,
        Guid customerId,
        Guid monthlyDistributionSchemeId,
        Guid? participantId,
        int reminderType,
        CancellationToken cancellationToken = default);
}
