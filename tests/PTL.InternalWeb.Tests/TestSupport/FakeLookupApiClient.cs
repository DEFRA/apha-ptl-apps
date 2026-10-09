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
    public IReadOnlyList<YearResponse> AllYears { get; set; } = [];
    public IReadOnlyList<YearResponse> WeightedPricingYears { get; set; } = [];
    public IReadOnlyList<SchemeCurrencyResponse> SchemeCurrencies { get; set; } = [];
    public IReadOnlyList<PostagePricingPlanResponse> PostagePricingPlans { get; set; } = [];
    public SystemSettingsResponse SystemSettings { get; set; } = new(string.Empty, new DateTime(2025, 4, 1));
    public IReadOnlyList<GroupAddressResponse> GroupAddresses { get; set; } = [];

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

    public Task<IReadOnlyList<YearResponse>> GetAllYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AllYears);

    public Task<IReadOnlyList<YearResponse>> GetWeightedPricingYearsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(WeightedPricingYears);

    public Task<IReadOnlyList<SchemeCurrencyResponse>> GetSchemeCurrenciesAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SchemeCurrencies);

    public Task<IReadOnlyList<PostagePricingPlanResponse>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PostagePricingPlans);

    public Task<SystemSettingsResponse> GetSystemSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SystemSettings);

    public Task<IReadOnlyList<GroupAddressResponse>> GetGroupAddressesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(GroupAddresses);

    public IReadOnlyList<ScheduleResponse> Schedules { get; set; } = [];
    public IReadOnlyList<ScheduleCodeResponse> ScheduleCodes { get; set; } = [];
    public IReadOnlyList<DayResponse> Days { get; set; } = [];
    public PTL.Contracts.Scheme.SchemeMonthEditabilityResponse SchemeMonthEditability { get; set; } =
        new(true, true, true, true, true, true, true, true, true, true, true, true);

    public Task<PTL.Contracts.Scheme.SchemeMonthEditabilityResponse> GetSchemeMonthEditabilityAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SchemeMonthEditability);

    public Task<IReadOnlyList<ScheduleResponse>> GetSchedulesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Schedules);

    public Task<IReadOnlyList<ScheduleCodeResponse>> GetScheduleCodesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ScheduleCodes);

    public Task<IReadOnlyList<DayResponse>> GetDaysAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Days);

    public IReadOnlyList<SchemeUserResponse> TestConsultants { get; set; } = [];
    public IReadOnlyList<SchemeUserResponse> Assessors { get; set; } = [];
    public IReadOnlyList<PTL.Contracts.Participant.ViewerResponse> Viewers { get; set; } = [];

    public Task<IReadOnlyList<SchemeUserResponse>> GetTestConsultantsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(TestConsultants);

    public Task<IReadOnlyList<SchemeUserResponse>> GetAssessorsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Assessors);

    public Task<IReadOnlyList<PTL.Contracts.Participant.ViewerResponse>> GetViewersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Viewers);

    public IReadOnlyList<SchemeItemTypeResponse> SchemeItemTypes { get; set; } = [];

    public Task<IReadOnlyList<SchemeItemTypeResponse>> GetSchemeItemTypesAsync(SchemeItemTypeKind kind, int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SchemeItemTypes);
}
