using PTL.ApiClient;
using PTL.Contracts.Search;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for ISearchApiClient so Search tests don't need a real HTTP call to PTL.Api.
internal sealed class FakeSearchApiClient : ISearchApiClient
{
    public IReadOnlyList<CustomerSearchResultResponse> CustomerResults { get; set; } = [];

    public IReadOnlyList<ParticipantSearchResultResponse> ParticipantResults { get; set; } = [];

    public List<CustomerSearchCriteriaRequest> CustomerSearches { get; } = [];

    public List<ParticipantSearchCriteriaRequest> ParticipantSearches { get; } = [];

    public IReadOnlyList<SchemeNameSearchResultResponse> SchemeResults { get; set; } = [];

    public List<SchemeNameSearchCriteriaRequest> SchemeSearches { get; } = [];

    public Task<IReadOnlyList<SchemeNameSearchResultResponse>> SearchSchemesByNameAsync(SchemeNameSearchCriteriaRequest criteria, CancellationToken cancellationToken = default)
    {
        SchemeSearches.Add(criteria);
        return Task.FromResult(SchemeResults);
    }

    public Task<IReadOnlyList<CustomerSearchResultResponse>> SearchCustomersAsync(CustomerSearchCriteriaRequest criteria, CancellationToken cancellationToken = default)
    {
        CustomerSearches.Add(criteria);
        return Task.FromResult(CustomerResults);
    }

    public Task<IReadOnlyList<ParticipantSearchResultResponse>> SearchParticipantsAsync(ParticipantSearchCriteriaRequest criteria, CancellationToken cancellationToken = default)
    {
        ParticipantSearches.Add(criteria);
        return Task.FromResult(ParticipantResults);
    }
}
