namespace PTL.Core.Search;

public interface ISearchRepository
{
    // dbo.spgSearchCustomer - rows come back in the procedure's own order.
    Task<IReadOnlyList<CustomerSearchRow>> SearchCustomersAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default);

    // dbo.spgSearchParticipant - one row per matching old lab code, as in legacy.
    Task<IReadOnlyList<ParticipantSearchRow>> SearchParticipantsAsync(ParticipantSearchCriteria criteria, CancellationToken cancellationToken = default);

    // dbo.spgSearchSchemeByDistribution - rows come back ordered by distribution number, as in legacy.
    Task<IReadOnlyList<DistributionNumberSearchRow>> SearchByDistributionNumberAsync(DistributionNumberSearchCriteria criteria, CancellationToken cancellationToken = default);

    // dbo.spgSearchSchemeBySampleNumber - rows come back ordered by sample number, as in legacy.
    Task<IReadOnlyList<SampleNumberSearchRow>> SearchBySampleNumberAsync(SampleNumberSearchCriteria criteria, CancellationToken cancellationToken = default);
}
