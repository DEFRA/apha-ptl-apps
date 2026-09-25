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

    // spgaYear - every year on record, ordered newest first. Needed to render the Year column on
    // the Contract list (legacy GetYearNameFromYearId), since historical contracts can reference
    // any past year, not just current/next.
    Task<IReadOnlyList<YearEntity>> GetAllYearsAsync(CancellationToken cancellationToken = default);

    // spgaSchemeCurrency - all scheme-currency pricing rows; SchemeService filters by SchemeId
    // (see docs/analysis/scheme-analysis.md, "Scheme Currency Read Operations").
    Task<IReadOnlyList<SchemeCurrencyEntity>> GetSchemeCurrenciesAsync(CancellationToken cancellationToken = default);

    // spgPostageByYearID - postage pricing plans for a given year.
    Task<IReadOnlyList<PostagePricingPlanEntity>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default);

    // spgaSystemSettings - single-row system settings (only UTNumber is modelled).
    Task<SystemSettingsEntity> GetSystemSettingsAsync(CancellationToken cancellationToken = default);

    // spgaWeightedPricingYear - years for which a weighted-pricing plan (tblPricingPercentage) is
    // configured; matches legacy WeightedPricingYearCollection.PricingPlanExists(yearId) used by
    // ParticipantScheme.aspx.vb's LoadPricingOptions to decide which Pricing Plan options to offer.
    Task<IReadOnlyList<YearEntity>> GetWeightedPricingYearsAsync(CancellationToken cancellationToken = default);

    // spgaGroupAddress - matches legacy GroupAddressCollection.FetchGroupAddressCollection(), used
    // by ParticipantScheme.aspx's "Select a Group Address" popup.
    Task<IReadOnlyList<GroupAddressEntity>> GetGroupAddressesAsync(CancellationToken cancellationToken = default);
}
