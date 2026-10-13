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
    public CountrySaveRequest? LastCreateRequest { get; private set; }
    public (Guid CountryId, CountrySaveRequest Request)? LastUpdateRequest { get; private set; }
    public Guid? LastDeletedCountryId { get; private set; }

    public Task<IReadOnlyList<CountryResponse>> GetCountriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Countries);

    public Task<IReadOnlyList<CountryTypeResponse>> GetCountryTypesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CountryTypes);

    public Task<CountrySaveResult> CreateCountryAsync(CountrySaveRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;
        return Task.FromResult(SaveResult);
    }

    public Task<CountrySaveResult> UpdateCountryAsync(Guid countryId, CountrySaveRequest request, CancellationToken cancellationToken = default)
    {
        LastUpdateRequest = (countryId, request);
        return Task.FromResult(SaveResult);
    }

    public Task<CountryDeleteResponse> DeleteCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        LastDeletedCountryId = countryId;
        return Task.FromResult(DeleteResponse);
    }
}
