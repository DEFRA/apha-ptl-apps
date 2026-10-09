using PTL.Contracts.Lookup;
using PTL.Core.Scheme;

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

    public Task<IReadOnlyList<YearEntity>> GetAllYearsAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetAllYearsAsync(cancellationToken);

    public Task<IReadOnlyList<YearEntity>> GetWeightedPricingYearsAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetWeightedPricingYearsAsync(cancellationToken);

    public Task<IReadOnlyList<GroupAddressEntity>> GetGroupAddressesAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetGroupAddressesAsync(cancellationToken);

    public Task<IReadOnlyList<ScheduleEntity>> GetSchedulesAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetSchedulesAsync(cancellationToken);

    public Task<IReadOnlyList<ScheduleCodeEntity>> GetScheduleCodesAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetScheduleCodesAsync(cancellationToken);

    public async Task<SchemeMonthEditability> GetSchemeMonthEditabilityAsync(int yearId, CancellationToken cancellationToken = default)
    {
        var settings = await lookupRepository.GetSystemSettingsAsync(cancellationToken);
        var distributions = await lookupRepository.GetMonthlyDistributionsAsync(cancellationToken);
        return SchemeDistributionMonths.CalculateEditability(yearId, settings.ContractStartDate.Month, distributions);
    }

    public Task<IReadOnlyList<DayEntity>> GetDaysAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetDaysAsync(cancellationToken);

    public Task<IReadOnlyList<PTNumberEntity>> GetPTNumbersAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetPTNumbersAsync(cancellationToken);

    // Legacy Scheme.aspx.vb drops inactive people from both dropdown lists.
    public async Task<IReadOnlyList<SchemeUserEntity>> GetTestConsultantsAsync(CancellationToken cancellationToken = default)
    {
        var consultants = await lookupRepository.GetTestConsultantsAsync(cancellationToken);
        return consultants.Where(c => !c.IsInactive).ToList();
    }

    public async Task<IReadOnlyList<SchemeUserEntity>> GetAssessorsAsync(CancellationToken cancellationToken = default)
    {
        var assessors = await lookupRepository.GetAssessorsAsync(cancellationToken);
        return assessors.Where(a => !a.IsInactive).ToList();
    }

    public Task<IReadOnlyList<SchemeItemTypeEntity>> GetSchemeItemTypesAsync(SchemeItemTypeKind kind, int yearId, CancellationToken cancellationToken = default) =>
        lookupRepository.GetSchemeItemTypesAsync(kind, yearId, cancellationToken);

    public async Task<IReadOnlyList<SchemeCurrencyEntity>> GetSchemeCurrenciesAsync(Guid schemeId, CancellationToken cancellationToken = default)
    {
        var all = await lookupRepository.GetSchemeCurrenciesAsync(cancellationToken);
        return all.Where(c => c.SchemeId == schemeId).ToList();
    }

    public Task<IReadOnlyList<PostagePricingPlanEntity>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        lookupRepository.GetPostagePricingPlansForYearAsync(yearId, cancellationToken);

    public Task<SystemSettingsEntity> GetSystemSettingsAsync(CancellationToken cancellationToken = default) =>
        lookupRepository.GetSystemSettingsAsync(cancellationToken);
}
