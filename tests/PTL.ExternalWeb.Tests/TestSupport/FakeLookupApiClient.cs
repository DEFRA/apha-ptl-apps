using PTL.ApiClient;
using PTL.Contracts.Lookup;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Test double for ILookupApiClient so CustomerController tests don't need a real HTTP call to
// PTL.Api. Only GetCountriesAsync is exercised by the Edit Customer Details screen.
internal sealed class FakeLookupApiClient : ILookupApiClient
{
    public IReadOnlyList<CountryResponse> Countries { get; set; } = [];

    public Task<IReadOnlyList<CountryResponse>> GetCountriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Countries);

    public Task<IReadOnlyList<CurrencyResponse>> GetCurrenciesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<CustomerTypeResponse>> GetCustomerTypesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<VatRatingResponse>> GetVatRatingsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<LabTypeResponse>> GetLabTypesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<YearResponse>> GetCurrentYearsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<YearResponse>> GetAllYearsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<YearResponse>> GetWeightedPricingYearsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<GroupAddressResponse>> GetGroupAddressesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SchemeCurrencyResponse>> GetSchemeCurrenciesAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ScheduleResponse>> GetSchedulesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ScheduleCodeResponse>> GetScheduleCodesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<DayResponse>> GetDaysAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PTL.Contracts.Scheme.SchemeMonthEditabilityResponse> GetSchemeMonthEditabilityAsync(int yearId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SchemeUserResponse>> GetTestConsultantsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SchemeUserResponse>> GetAssessorsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<PTL.Contracts.Participant.ViewerResponse>> GetViewersAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<SchemeItemTypeResponse>> GetSchemeItemTypesAsync(SchemeItemTypeKind kind, int yearId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<PostagePricingPlanResponse>> GetPostagePricingPlansForYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SystemSettingsResponse> GetSystemSettingsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<TestReturnValueTypeResponse>> GetTestReturnValueTypesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
