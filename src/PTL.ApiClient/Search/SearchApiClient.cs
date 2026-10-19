using System.Net.Http.Json;
using PTL.Contracts.Search;

namespace PTL.ApiClient;

public interface ISearchApiClient
{
    Task<IReadOnlyList<CustomerSearchResultResponse>> SearchCustomersAsync(CustomerSearchCriteriaRequest criteria, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParticipantSearchResultResponse>> SearchParticipantsAsync(ParticipantSearchCriteriaRequest criteria, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's search endpoints.
public sealed class SearchApiClient(HttpClient httpClient) : ISearchApiClient
{
    public async Task<IReadOnlyList<CustomerSearchResultResponse>> SearchCustomersAsync(CustomerSearchCriteriaRequest criteria, CancellationToken cancellationToken = default)
    {
        var query = string.Join(
            "&",
            $"qalNumber={Escape(criteria.QalNumber)}",
            $"accountNumber={Escape(criteria.AccountNumber)}",
            $"name={Escape(criteria.Name)}",
            $"contactName={Escape(criteria.ContactName)}",
            $"organisation={Escape(criteria.Organisation)}",
            $"countryId={criteria.CountryId}");

        var results = await httpClient.GetFromJsonAsync<IReadOnlyList<CustomerSearchResultResponse>>($"/api/search/customers?{query}", cancellationToken);
        return results ?? [];
    }

    public async Task<IReadOnlyList<ParticipantSearchResultResponse>> SearchParticipantsAsync(ParticipantSearchCriteriaRequest criteria, CancellationToken cancellationToken = default)
    {
        var query = $"labCode={Escape(criteria.LabCode)}&labName={Escape(criteria.LabName)}&contactName={Escape(criteria.ContactName)}";

        var results = await httpClient.GetFromJsonAsync<IReadOnlyList<ParticipantSearchResultResponse>>($"/api/search/participants?{query}", cancellationToken);
        return results ?? [];
    }

    private static string Escape(string? value) => Uri.EscapeDataString(value ?? string.Empty);
}
