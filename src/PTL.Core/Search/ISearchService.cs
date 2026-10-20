using PTL.Contracts.Search;

namespace PTL.Core.Search;

public interface ISearchService
{
    Task<IReadOnlyList<CustomerSearchRow>> SearchCustomersAsync(CustomerSearchCriteriaRequest criteria, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParticipantSearchRow>> SearchParticipantsAsync(ParticipantSearchCriteriaRequest criteria, CancellationToken cancellationToken = default);
}
