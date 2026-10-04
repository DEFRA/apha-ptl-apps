using PTL.Core.Lookup;

namespace PTL.Api.Tests.Invoice;

// In-memory ILookupService test double scoped to InvoiceControllerTests - see
// WeightedPricingPlan.FakeLookupServiceForWeightedPricingPlan for the pattern this follows.
internal sealed class FakeLookupServiceForInvoice : ILookupService
{
    public IReadOnlyList<YearEntity> CurrentYears { get; set; } = [];

    public Task<IReadOnlyList<CountryEntity>> GetCountriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CountryEntity>>([]);
    public Task<IReadOnlyList<CurrencyEntity>> GetCurrenciesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CurrencyEntity>>([]);
    public Task<IReadOnlyList<CustomerTypeEntity>> GetCustomerTypesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerTypeEntity>>([]);
    public Task<IReadOnlyList<VatRatingEntity>> GetVatRatingsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<VatRatingEntity>>([]);
    public Task<IReadOnlyList<LabTypeEntity>> GetLabTypesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LabTypeEntity>>([]);
    public Task<IReadOnlyList<YearEntity>> GetCurrentYearsAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentYears);
    public Task<IReadOnlyList<YearEntity>> GetAllYearsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<YearEntity>>([]);
    public Task<IReadOnlyList<YearEntity>> GetWeightedPricingYearsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<YearEntity>>([]);
    public Task<IReadOnlyList<GroupAddressEntity>> GetGroupAddressesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GroupAddressEntity>>([]);
    public Task<IReadOnlyList<SchemeCurrencyEntity>> GetSchemeCurrenciesAsync(Guid schemeId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SchemeCurrencyEntity>>([]);
    public Task<IReadOnlyList<PostagePricingPlanEntity>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PostagePricingPlanEntity>>([]);
    public Task<SystemSettingsEntity> GetSystemSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SystemSettingsEntity());
}
