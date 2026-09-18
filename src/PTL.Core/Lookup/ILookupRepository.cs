namespace PTL.Core.Lookup;

// Defined in Core (not Data) so ILookupService can depend on the abstraction without Core
// referencing Data; PTL.Data.Lookup.LookupRepository implements this. These are read-only
// reference lists (Country/Currency/CustomerType/VatRating/LabType/Year) with no create/update/
// delete in any screen.
public interface ILookupRepository
{
    Task<IReadOnlyList<CountryEntity>> GetCountriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CurrencyEntity>> GetCurrenciesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerTypeEntity>> GetCustomerTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VatRatingEntity>> GetVatRatingsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LabTypeEntity>> GetLabTypesAsync(CancellationToken cancellationToken = default);

    // spgaYearCurrent - current + next year only, matching the legacy Contract.aspx
    // SetYearDropDown() behaviour for creating/editing a contract.
    Task<IReadOnlyList<YearEntity>> GetCurrentYearsAsync(CancellationToken cancellationToken = default);
}
