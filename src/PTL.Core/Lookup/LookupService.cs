namespace PTL.Core.Lookup;

// Thin passthrough - these lookups have no business rules to apply, only a repository to hide.
public sealed class LookupService(ILookupRepository lookupRepository) : ILookupService
{
    public Task<IReadOnlyList<CountryEntity>> GetCountriesAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetCountriesAsync(cancellationToken);

    public Task<IReadOnlyList<CurrencyEntity>> GetCurrenciesAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetCurrenciesAsync(cancellationToken);

    public Task<IReadOnlyList<CustomerTypeEntity>> GetCustomerTypesAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetCustomerTypesAsync(cancellationToken);

    public Task<IReadOnlyList<VatRatingEntity>> GetVatRatingsAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetVatRatingsAsync(cancellationToken);

    public Task<IReadOnlyList<LabTypeEntity>> GetLabTypesAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetLabTypesAsync(cancellationToken);

    public Task<IReadOnlyList<YearEntity>> GetCurrentYearsAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetCurrentYearsAsync(cancellationToken);
}
