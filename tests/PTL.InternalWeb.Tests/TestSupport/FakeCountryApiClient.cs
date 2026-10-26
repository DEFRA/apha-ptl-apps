using PTL.ApiClient;
using PTL.Contracts.Country;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for ICountryApiClient so SystemAdministrationController tests don't need a real
// HTTP call to PTL.Api.
internal sealed class FakeCountryApiClient : ICountryApiClient
{
    public IReadOnlyList<CountryResponse> Countries { get; set; } = [];
    public IReadOnlyList<CountryTypeResponse> CountryTypes { get; set; } = [];
    public CountrySaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());
    public CountryDeleteResponse DeleteResponse { get; set; } = new(true, null);
    public List<CountrySaveRequest> CreateRequests { get; } = [];
    public List<(Guid CountryId, CountrySaveRequest Request)> UpdateRequests { get; } = [];
    public List<Guid> DeleteCalls { get; } = [];

    // When set, decides the result per request instead of SaveResult.
    public Func<CountrySaveRequest, CountrySaveResult>? SaveResultFor { get; set; }

    // When set, decides the delete result per country instead of DeleteResponse.
    public Func<Guid, CountryDeleteResponse>? DeleteResultFor { get; set; }

    public Task<IReadOnlyList<CountryResponse>> GetCountriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Countries);

    public Task<IReadOnlyList<CountryTypeResponse>> GetCountryTypesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CountryTypes);

    public Task<CountrySaveResult> CreateCountryAsync(CountrySaveRequest request, CancellationToken cancellationToken = default)
    {
        CreateRequests.Add(request);
        return Task.FromResult(SaveResultFor?.Invoke(request) ?? SaveResult);
    }

    public Task<CountrySaveResult> UpdateCountryAsync(Guid countryId, CountrySaveRequest request, CancellationToken cancellationToken = default)
    {
        UpdateRequests.Add((countryId, request));
        return Task.FromResult(SaveResultFor?.Invoke(request) ?? SaveResult);
    }

    public Task<CountryDeleteResponse> DeleteCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(countryId);
        return Task.FromResult(DeleteResultFor?.Invoke(countryId) ?? DeleteResponse);
    }
}
