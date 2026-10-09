using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PTL.Contracts.Country;

namespace PTL.ApiClient;

public interface ICountryApiClient
{
    Task<IReadOnlyList<CountryResponse>> GetCountriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CountryTypeResponse>> GetCountryTypesAsync(CancellationToken cancellationToken = default);
    Task<CountrySaveResult> CreateCountryAsync(CountrySaveRequest request, CancellationToken cancellationToken = default);
    Task<CountrySaveResult> UpdateCountryAsync(Guid countryId, CountrySaveRequest request, CancellationToken cancellationToken = default);
    Task<CountryDeleteResponse> DeleteCountryAsync(Guid countryId, CancellationToken cancellationToken = default);
}

public sealed class CountryApiClient(HttpClient httpClient) : ICountryApiClient
{
    public async Task<IReadOnlyList<CountryResponse>> GetCountriesAsync(CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<CountryResponse>>("/api/countries", cancellationToken);
        return items ?? [];
    }

    public async Task<IReadOnlyList<CountryTypeResponse>> GetCountryTypesAsync(CancellationToken cancellationToken = default)
    {
        var items = await httpClient.GetFromJsonAsync<IReadOnlyList<CountryTypeResponse>>("/api/countries/types", cancellationToken);
        return items ?? [];
    }

    public async Task<CountrySaveResult> CreateCountryAsync(CountrySaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/countries", request, cancellationToken);
        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<CountrySaveResult> UpdateCountryAsync(Guid countryId, CountrySaveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/countries/{countryId}", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new CountrySaveResult(false, null, new Dictionary<string, string[]> { [string.Empty] = ["Country was not found."] });
        }

        return await ToSaveResultAsync(response, cancellationToken);
    }

    public async Task<CountryDeleteResponse> DeleteCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/countries/{countryId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CountryDeleteResponse>(cancellationToken);
        return result ?? new CountryDeleteResponse(false, "The country could not be removed.");
    }

    private static async Task<CountrySaveResult> ToSaveResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var errors = problem?.Errors is { Count: > 0 }
                ? problem.Errors.ToDictionary(e => e.Key, e => e.Value)
                : new Dictionary<string, string[]> { [string.Empty] = ["The request was invalid."] };
            return new CountrySaveResult(false, null, errors);
        }

        response.EnsureSuccessStatusCode();
        var country = await response.Content.ReadFromJsonAsync<CountryResponse>(cancellationToken);
        return new CountrySaveResult(true, country, new Dictionary<string, string[]>());
    }
}
