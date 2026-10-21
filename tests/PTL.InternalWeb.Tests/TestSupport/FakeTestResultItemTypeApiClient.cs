using PTL.ApiClient;
using PTL.Contracts.TestResultItemType;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for ITestResultItemTypeApiClient so SchemeAdminController tests don't need a real
// HTTP call to PTL.Api. Mirrors FakeTestTypeApiClient, with an added Search parameter capture.
internal sealed class FakeTestResultItemTypeApiClient : ITestResultItemTypeApiClient
{
    public IReadOnlyList<TestResultItemTypeResponse> Items { get; set; } = [];
    public TestResultItemTypeSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());
    public TestResultItemTypeDeleteResponse DeleteResponse { get; set; } = new(true, null);
    public TestResultItemTypeSaveRequest? LastCreateRequest { get; private set; }
    public (Guid TestResultItemTypeId, TestResultItemTypeSaveRequest Request)? LastUpdateRequest { get; private set; }
    public List<(Guid TestResultItemTypeId, TestResultItemTypeNoLongerInUseRequest Request)> AllNoLongerInUseRequests { get; } = [];
    public Guid? LastDeletedTestResultItemTypeId { get; private set; }
    public int? LastDeletedYearId { get; private set; }
    public string? LastSearch { get; private set; }

    public Task<IReadOnlyList<TestResultItemTypeResponse>> GetByYearAsync(int yearId, string? search = null, CancellationToken cancellationToken = default)
    {
        LastSearch = search;
        return Task.FromResult(Items);
    }

    public Task<TestResultItemTypeSaveResult> CreateAsync(TestResultItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;
        return Task.FromResult(SaveResult);
    }

    public Task<TestResultItemTypeSaveResult> UpdateAsync(Guid testResultItemTypeId, TestResultItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastUpdateRequest = (testResultItemTypeId, request);
        return Task.FromResult(SaveResult);
    }

    public Task<TestResultItemTypeSaveResult> SetNoLongerInUseAsync(Guid testResultItemTypeId, TestResultItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default)
    {
        AllNoLongerInUseRequests.Add((testResultItemTypeId, request));
        return Task.FromResult(SaveResult);
    }

    public Task<TestResultItemTypeDeleteResponse> DeleteAsync(Guid testResultItemTypeId, int yearId, CancellationToken cancellationToken = default)
    {
        LastDeletedTestResultItemTypeId = testResultItemTypeId;
        LastDeletedYearId = yearId;
        return Task.FromResult(DeleteResponse);
    }
}
