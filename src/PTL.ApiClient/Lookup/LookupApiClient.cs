using System.Net.Http.Json;
using PTL.Contracts.Lookup;

namespace PTL.ApiClient;

public interface ILookupApiClient
{
    Task<IReadOnlyList<CountryResponse>> GetCountriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CurrencyResponse>> GetCurrenciesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerTypeResponse>> GetCustomerTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VatRatingResponse>> GetVatRatingsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LabTypeResponse>> GetLabTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<YearResponse>> GetCurrentYearsAsync(CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around PTL.Api's read-only lookup endpoints, shared by every web
// front-end (PTL.InternalWeb, PTL.ExternalWeb, ...).
public sealed class LookupApiClient(HttpClient httpClient) : ILookupApiClient
{
    public async Task<IReadOnlyList<CountryResponse>> GetCountriesAsync(CancellationToken cancellationToken = default)
    {
        var countries = await httpClient.GetFromJsonAsync<IReadOnlyList<CountryResponse>>("/api/lookups/countries", cancellationToken);
        return countries ?? [];
    }

    public async Task<IReadOnlyList<CurrencyResponse>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        var currencies = await httpClient.GetFromJsonAsync<IReadOnlyList<CurrencyResponse>>("/api/lookups/currencies", cancellationToken);
        return currencies ?? [];
    }

    public async Task<IReadOnlyList<CustomerTypeResponse>> GetCustomerTypesAsync(CancellationToken cancellationToken = default)
    {
        var customerTypes = await httpClient.GetFromJsonAsync<IReadOnlyList<CustomerTypeResponse>>("/api/lookups/customer-types", cancellationToken);
        return customerTypes ?? [];
    }

    public async Task<IReadOnlyList<VatRatingResponse>> GetVatRatingsAsync(CancellationToken cancellationToken = default)
    {
        var vatRatings = await httpClient.GetFromJsonAsync<IReadOnlyList<VatRatingResponse>>("/api/lookups/vat-ratings", cancellationToken);
        return vatRatings ?? [];
    }

    public async Task<IReadOnlyList<LabTypeResponse>> GetLabTypesAsync(CancellationToken cancellationToken = default)
    {
        var labTypes = await httpClient.GetFromJsonAsync<IReadOnlyList<LabTypeResponse>>("/api/lookups/lab-types", cancellationToken);
        return labTypes ?? [];
    }

    public async Task<IReadOnlyList<YearResponse>> GetCurrentYearsAsync(CancellationToken cancellationToken = default)
    {
        var years = await httpClient.GetFromJsonAsync<IReadOnlyList<YearResponse>>("/api/lookups/years", cancellationToken);
        return years ?? [];
    }
}
