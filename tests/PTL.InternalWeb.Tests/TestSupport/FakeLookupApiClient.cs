using PTL.ApiClient;
using PTL.Contracts.Lookup;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for ILookupApiClient so CustomerController tests don't need a real HTTP call
// to PTL.Api. Returns empty lists by default (SelectListItem population is not the concern
// of these tests).
internal sealed class FakeLookupApiClient : ILookupApiClient
{
    public IReadOnlyList<CountryResponse> Countries { get; set; } = [];
    public IReadOnlyList<CurrencyResponse> Currencies { get; set; } = [];
    public IReadOnlyList<CustomerTypeResponse> CustomerTypes { get; set; } = [];
    public IReadOnlyList<VatRatingResponse> VatRatings { get; set; } = [];
    public IReadOnlyList<LabTypeResponse> LabTypes { get; set; } = [];
    public IReadOnlyList<YearResponse> Years { get; set; } = [];
    public IReadOnlyList<SchemeCurrencyResponse> SchemeCurrencies { get; set; } = [];
    public IReadOnlyList<PostagePricingPlanResponse> PostagePricingPlans { get; set; } = [];
    public SystemSettingsResponse SystemSettings { get; set; } = new(string.Empty);

    public Task<IReadOnlyList<CountryResponse>> GetCountriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Countries);

    public Task<IReadOnlyList<CurrencyResponse>> GetCurrenciesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Currencies);

    public Task<IReadOnlyList<CustomerTypeResponse>> GetCustomerTypesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomerTypes);

    public Task<IReadOnlyList<VatRatingResponse>> GetVatRatingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(VatRatings);

    public Task<IReadOnlyList<LabTypeResponse>> GetLabTypesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(LabTypes);

    public Task<IReadOnlyList<YearResponse>> GetCurrentYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Years);

    public Task<IReadOnlyList<SchemeCurrencyResponse>> GetSchemeCurrenciesAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SchemeCurrencies);

    public Task<IReadOnlyList<PostagePricingPlanResponse>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PostagePricingPlans);

    public Task<SystemSettingsResponse> GetSystemSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SystemSettings);
}
