using PTL.Contracts.Search;

namespace PTL.Core.Search;

public interface ISearchService
{
    Task<IReadOnlyList<CustomerSearchRow>> SearchCustomersAsync(CustomerSearchCriteriaRequest criteria, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParticipantSearchRow>> SearchParticipantsAsync(ParticipantSearchCriteriaRequest criteria, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistributionNumberSearchRow>> SearchByDistributionNumberAsync(DistributionNumberSearchCriteriaRequest criteria, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SampleNumberSearchRow>> SearchBySampleNumberAsync(SampleNumberSearchCriteriaRequest criteria, CancellationToken cancellationToken = default);
}
