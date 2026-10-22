using System.Net.Http.Json;
using PTL.Contracts.Search;

namespace PTL.ApiClient.Distribution;

// Legacy Distributions/Search.aspx - Distribution Number / Sample Number tabs. Calls PTL.Api's
// generic search endpoints (api/search/...), which already host every legacy Search.aspx-style
// lookup (ISearchRepository/ISearchService) - no separate Distribution search infrastructure.
public partial interface IDistributionApiClient
{
    Task<IReadOnlyList<DistributionNumberSearchResultResponse>> SearchByDistributionNumberAsync(
        DistributionNumberSearchCriteriaRequest criteria, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SampleNumberSearchResultResponse>> SearchBySampleNumberAsync(
        SampleNumberSearchCriteriaRequest criteria, CancellationToken cancellationToken = default);
}

public sealed partial class DistributionApiClient
{
    public async Task<IReadOnlyList<DistributionNumberSearchResultResponse>> SearchByDistributionNumberAsync(
        DistributionNumberSearchCriteriaRequest criteria, CancellationToken cancellationToken = default)
    {
        var query = $"distributionNumber={Escape(criteria.DistributionNumber)}";
        var results = await httpClient.GetFromJsonAsync<IReadOnlyList<DistributionNumberSearchResultResponse>>(
            $"/api/search/distributions?{query}", cancellationToken);
        return results ?? [];
    }

    public async Task<IReadOnlyList<SampleNumberSearchResultResponse>> SearchBySampleNumberAsync(
        SampleNumberSearchCriteriaRequest criteria, CancellationToken cancellationToken = default)
    {
        var query = $"sampleNumber={Escape(criteria.SampleNumber)}";
        var results = await httpClient.GetFromJsonAsync<IReadOnlyList<SampleNumberSearchResultResponse>>(
            $"/api/search/samples?{query}", cancellationToken);
        return results ?? [];
    }

    private static string Escape(string? value) => Uri.EscapeDataString(value ?? string.Empty);
}
