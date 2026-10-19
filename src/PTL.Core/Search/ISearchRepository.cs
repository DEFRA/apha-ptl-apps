namespace PTL.Core.Search;

public interface ISearchRepository
{
    // dbo.spgSearchCustomer - rows come back in the procedure's own order.
    Task<IReadOnlyList<CustomerSearchRow>> SearchCustomersAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default);

    // dbo.spgSearchParticipant - one row per matching old lab code, as in legacy.
    Task<IReadOnlyList<ParticipantSearchRow>> SearchParticipantsAsync(ParticipantSearchCriteria criteria, CancellationToken cancellationToken = default);
}
