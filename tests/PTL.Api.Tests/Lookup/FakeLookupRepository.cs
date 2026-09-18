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
}
