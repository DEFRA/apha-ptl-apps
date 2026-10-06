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
    Task<IReadOnlyList<YearResponse>> GetAllYearsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<YearResponse>> GetWeightedPricingYearsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GroupAddressResponse>> GetGroupAddressesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchemeCurrencyResponse>> GetSchemeCurrenciesAsync(Guid schemeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduleResponse>> GetSchedulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduleCodeResponse>> GetScheduleCodesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DayResponse>> GetDaysAsync(CancellationToken cancellationToken = default);
    Task<PTL.Contracts.Scheme.SchemeMonthEditabilityResponse> GetSchemeMonthEditabilityAsync(int yearId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchemeUserResponse>> GetTestConsultantsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchemeUserResponse>> GetAssessorsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PTL.Contracts.Participant.ViewerResponse>> GetViewersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchemeItemTypeResponse>> GetSchemeItemTypesAsync(SchemeItemTypeKind kind, int yearId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PostagePricingPlanResponse>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default);
    Task<SystemSettingsResponse> GetSystemSettingsAsync(CancellationToken cancellationToken = default);
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

    public async Task<IReadOnlyList<YearResponse>> GetAllYearsAsync(CancellationToken cancellationToken = default)
    {
        var years = await httpClient.GetFromJsonAsync<IReadOnlyList<YearResponse>>("/api/lookups/years/all", cancellationToken);
        return years ?? [];
    }

    public async Task<IReadOnlyList<YearResponse>> GetWeightedPricingYearsAsync(CancellationToken cancellationToken = default)
    {
        var years = await httpClient.GetFromJsonAsync<IReadOnlyList<YearResponse>>("/api/lookups/years/weighted-pricing", cancellationToken);
        return years ?? [];
    }

    public async Task<IReadOnlyList<GroupAddressResponse>> GetGroupAddressesAsync(CancellationToken cancellationToken = default)
    {
        var groupAddresses = await httpClient.GetFromJsonAsync<IReadOnlyList<GroupAddressResponse>>("/api/lookups/group-addresses", cancellationToken);
        return groupAddresses ?? [];
    }

    public async Task<IReadOnlyList<SchemeCurrencyResponse>> GetSchemeCurrenciesAsync(Guid schemeId, CancellationToken cancellationToken = default)
    {
        var currencies = await httpClient.GetFromJsonAsync<IReadOnlyList<SchemeCurrencyResponse>>($"/api/lookups/schemes/{schemeId}/currencies", cancellationToken);
        return currencies ?? [];
    }

    public async Task<IReadOnlyList<ScheduleResponse>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var schedules = await httpClient.GetFromJsonAsync<IReadOnlyList<ScheduleResponse>>("/api/lookups/schedules", cancellationToken);
        return schedules ?? [];
    }

    public async Task<IReadOnlyList<ScheduleCodeResponse>> GetScheduleCodesAsync(CancellationToken cancellationToken = default)
    {
        var scheduleCodes = await httpClient.GetFromJsonAsync<IReadOnlyList<ScheduleCodeResponse>>("/api/lookups/schedule-codes", cancellationToken);
        return scheduleCodes ?? [];
    }

    public async Task<IReadOnlyList<DayResponse>> GetDaysAsync(CancellationToken cancellationToken = default)
    {
        var days = await httpClient.GetFromJsonAsync<IReadOnlyList<DayResponse>>("/api/lookups/days", cancellationToken);
        return days ?? [];
    }

    public async Task<PTL.Contracts.Scheme.SchemeMonthEditabilityResponse> GetSchemeMonthEditabilityAsync(int yearId, CancellationToken cancellationToken = default)
    {
        var editability = await httpClient.GetFromJsonAsync<PTL.Contracts.Scheme.SchemeMonthEditabilityResponse>(
            $"/api/lookups/scheme-month-editability?year={yearId}", cancellationToken);
        return editability ?? new PTL.Contracts.Scheme.SchemeMonthEditabilityResponse(true, true, true, true, true, true, true, true, true, true, true, true);
    }

    public async Task<IReadOnlyList<SchemeUserResponse>> GetTestConsultantsAsync(CancellationToken cancellationToken = default)
    {
        var consultants = await httpClient.GetFromJsonAsync<IReadOnlyList<SchemeUserResponse>>("/api/lookups/test-consultants", cancellationToken);
        return consultants ?? [];
    }

    public async Task<IReadOnlyList<SchemeUserResponse>> GetAssessorsAsync(CancellationToken cancellationToken = default)
    {
        var assessors = await httpClient.GetFromJsonAsync<IReadOnlyList<SchemeUserResponse>>("/api/lookups/assessors", cancellationToken);
        return assessors ?? [];
    }

    public async Task<IReadOnlyList<PTL.Contracts.Participant.ViewerResponse>> GetViewersAsync(CancellationToken cancellationToken = default)
    {
        var viewers = await httpClient.GetFromJsonAsync<IReadOnlyList<PTL.Contracts.Participant.ViewerResponse>>("/api/lookups/viewers", cancellationToken);
        return viewers ?? [];
    }

    public async Task<IReadOnlyList<SchemeItemTypeResponse>> GetSchemeItemTypesAsync(SchemeItemTypeKind kind, int yearId, CancellationToken cancellationToken = default)
    {
        var itemTypes = await httpClient.GetFromJsonAsync<IReadOnlyList<SchemeItemTypeResponse>>(
            $"/api/lookups/scheme-item-types?kind={kind}&year={yearId}", cancellationToken);
        return itemTypes ?? [];
    }

    public async Task<IReadOnlyList<PostagePricingPlanResponse>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default)
    {
        var plans = await httpClient.GetFromJsonAsync<IReadOnlyList<PostagePricingPlanResponse>>($"/api/lookups/postage-pricing-plans?year={yearId}", cancellationToken);
        return plans ?? [];
    }

    public async Task<SystemSettingsResponse> GetSystemSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await httpClient.GetFromJsonAsync<SystemSettingsResponse>("/api/lookups/system-settings", cancellationToken);
        return settings ?? new SystemSettingsResponse(string.Empty);
    }
}
