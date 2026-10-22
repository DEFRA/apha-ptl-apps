using PTL.ApiClient;
using PTL.Contracts.TestMethodItemType;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for ITestMethodItemTypeApiClient so SchemeAdminController tests don't need a real
// HTTP call to PTL.Api. Mirrors FakeTestResultItemTypeApiClient.
internal sealed class FakeTestMethodItemTypeApiClient : ITestMethodItemTypeApiClient
{
    public IReadOnlyList<TestMethodItemTypeResponse> Items { get; set; } = [];
    public TestMethodItemTypeSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());
    public TestMethodItemTypeDeleteResponse DeleteResponse { get; set; } = new(true, null);
    public TestMethodItemTypeSaveRequest? LastCreateRequest { get; private set; }
    public (Guid TestMethodItemTypeId, TestMethodItemTypeSaveRequest Request)? LastUpdateRequest { get; private set; }
    public List<(Guid TestMethodItemTypeId, TestMethodItemTypeNoLongerInUseRequest Request)> AllNoLongerInUseRequests { get; } = [];
    public Guid? LastDeletedTestMethodItemTypeId { get; private set; }
    public int? LastDeletedYearId { get; private set; }
    public string? LastSearch { get; private set; }
    public Exception? SetNoLongerInUseException { get; set; }

    public Task<IReadOnlyList<TestMethodItemTypeResponse>> GetByYearAsync(int yearId, string? search = null, CancellationToken cancellationToken = default)
    {
        LastSearch = search;
        return Task.FromResult(Items);
    }

    public Task<TestMethodItemTypeSaveResult> CreateAsync(TestMethodItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;
        return Task.FromResult(SaveResult);
    }

    public Task<TestMethodItemTypeSaveResult> UpdateAsync(Guid testMethodItemTypeId, TestMethodItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastUpdateRequest = (testMethodItemTypeId, request);
        return Task.FromResult(SaveResult);
    }

    public Task<TestMethodItemTypeSaveResult> SetNoLongerInUseAsync(Guid testMethodItemTypeId, TestMethodItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default)
    {
        if (SetNoLongerInUseException is not null)
        {
            throw SetNoLongerInUseException;
        }

        AllNoLongerInUseRequests.Add((testMethodItemTypeId, request));
        return Task.FromResult(SaveResult);
    }

    public Task<TestMethodItemTypeDeleteResponse> DeleteAsync(Guid testMethodItemTypeId, int yearId, CancellationToken cancellationToken = default)
    {
        LastDeletedTestMethodItemTypeId = testMethodItemTypeId;
        LastDeletedYearId = yearId;
        return Task.FromResult(DeleteResponse);
    }
}
