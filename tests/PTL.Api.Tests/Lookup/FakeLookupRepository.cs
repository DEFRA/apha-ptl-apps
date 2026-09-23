using PTL.Core.Lookup;

namespace PTL.Api.Tests.Lookup;

// In-memory ILookupRepository test double so LookupService can be tested without a real
// database or the spgaCountry/spgaCurrency/spgaCustomerType stored procedures.
internal sealed class FakeLookupRepository : ILookupRepository
{
    public IReadOnlyList<CountryEntity> Countries { get; set; } = [];
    public IReadOnlyList<CurrencyEntity> Currencies { get; set; } = [];
    public IReadOnlyList<CustomerTypeEntity> CustomerTypes { get; set; } = [];
    public IReadOnlyList<VatRatingEntity> VatRatings { get; set; } = [];
    public IReadOnlyList<LabTypeEntity> LabTypes { get; set; } = [];
    public IReadOnlyList<YearEntity> Years { get; set; } = [];
    public IReadOnlyList<SchemeCurrencyEntity> SchemeCurrencies { get; set; } = [];
    public IReadOnlyList<PostagePricingPlanEntity> PostagePricingPlans { get; set; } = [];
    public SystemSettingsEntity SystemSettings { get; set; } = new();

    public Task<IReadOnlyList<CountryEntity>> GetCountriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Countries);

    public Task<IReadOnlyList<CurrencyEntity>> GetCurrenciesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Currencies);

    public Task<IReadOnlyList<CustomerTypeEntity>> GetCustomerTypesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomerTypes);

    public Task<IReadOnlyList<VatRatingEntity>> GetVatRatingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(VatRatings);

    public Task<IReadOnlyList<LabTypeEntity>> GetLabTypesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(LabTypes);

    public Task<IReadOnlyList<YearEntity>> GetCurrentYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Years);

    public Task<IReadOnlyList<SchemeCurrencyEntity>> GetSchemeCurrenciesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SchemeCurrencies);

    public Task<IReadOnlyList<PostagePricingPlanEntity>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PostagePricingPlans);

    public Task<SystemSettingsEntity> GetSystemSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SystemSettings);
}
