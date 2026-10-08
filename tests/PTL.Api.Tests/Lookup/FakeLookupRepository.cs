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
    public IReadOnlyList<YearEntity> AllYears { get; set; } = [];
    public IReadOnlyList<YearEntity> WeightedPricingYears { get; set; } = [];
    public IReadOnlyList<SchemeCurrencyEntity> SchemeCurrencies { get; set; } = [];
    public IReadOnlyList<PostagePricingPlanEntity> PostagePricingPlans { get; set; } = [];
    public SystemSettingsEntity SystemSettings { get; set; } = new();
    public IReadOnlyList<GroupAddressEntity> GroupAddresses { get; set; } = [];
    public IReadOnlyList<ScheduleEntity> Schedules { get; set; } = [];
    public IReadOnlyList<ScheduleCodeEntity> ScheduleCodes { get; set; } = [];
    public IReadOnlyList<DayEntity> Days { get; set; } = [];
    public IReadOnlyList<PTNumberEntity> PTNumbers { get; set; } = [];
    public IReadOnlyList<SchemeUserEntity> TestConsultants { get; set; } = [];
    public IReadOnlyList<SchemeUserEntity> Assessors { get; set; } = [];

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

    public Task<IReadOnlyList<YearEntity>> GetAllYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AllYears);

    public Task<IReadOnlyList<YearEntity>> GetWeightedPricingYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(WeightedPricingYears);

    public Task<IReadOnlyList<SchemeCurrencyEntity>> GetSchemeCurrenciesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SchemeCurrencies);

    public Task<IReadOnlyList<PostagePricingPlanEntity>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PostagePricingPlans);

    public Task<SystemSettingsEntity> GetSystemSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SystemSettings);

    public Task<IReadOnlyList<GroupAddressEntity>> GetGroupAddressesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(GroupAddresses);

    public Task<IReadOnlyList<ScheduleEntity>> GetSchedulesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Schedules);

    public Task<IReadOnlyList<ScheduleCodeEntity>> GetScheduleCodesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ScheduleCodes);

    public Task<IReadOnlyList<DayEntity>> GetDaysAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Days);

    public IReadOnlyList<MonthlyDistributionEntity> MonthlyDistributions { get; set; } = [];

    public Task<IReadOnlyList<MonthlyDistributionEntity>> GetMonthlyDistributionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(MonthlyDistributions);

    public Task<IReadOnlyList<PTNumberEntity>> GetPTNumbersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(PTNumbers);

    public Task<IReadOnlyList<SchemeUserEntity>> GetTestConsultantsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(TestConsultants);

    public Task<IReadOnlyList<SchemeUserEntity>> GetAssessorsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Assessors);

    public IReadOnlyList<SchemeItemTypeEntity> SchemeItemTypes { get; set; } = [];

    public Task<IReadOnlyList<SchemeItemTypeEntity>> GetSchemeItemTypesAsync(PTL.Contracts.Lookup.SchemeItemTypeKind kind, int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SchemeItemTypes);
}
