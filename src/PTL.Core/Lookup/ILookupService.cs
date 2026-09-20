namespace PTL.Core.Lookup;

public interface ILookupService
{
    Task<IReadOnlyList<CountryEntity>> GetCountriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CurrencyEntity>> GetCurrenciesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerTypeEntity>> GetCustomerTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VatRatingEntity>> GetVatRatingsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LabTypeEntity>> GetLabTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<YearEntity>> GetCurrentYearsAsync(CancellationToken cancellationToken = default);

    // Returns only the currency prices linked to the given scheme (spgaSchemeCurrency, filtered
    // in-memory - see docs/analysis/scheme-analysis.md, "Scheme Currency Read Operations").
    Task<IReadOnlyList<SchemeCurrencyEntity>> GetSchemeCurrenciesAsync(Guid schemeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PostagePricingPlanEntity>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default);
}
