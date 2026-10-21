using PTL.ApiClient;
using PTL.Contracts.TestType;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for ITestTypeApiClient so SchemeAdminController tests don't need a real HTTP call to
// PTL.Api. Mirrors FakeCategoryItemTypeApiClient, but DeleteAsync takes yearId (TestType's delete
// semantics are year-scoped, unlike Category Items).
internal sealed class FakeTestTypeApiClient : ITestTypeApiClient
{
    public IReadOnlyList<TestTypeResponse> Items { get; set; } = [];
    public TestTypeSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());
    public TestTypeDeleteResponse DeleteResponse { get; set; } = new(true, null);
    public TestTypeSaveRequest? LastCreateRequest { get; private set; }
    public (Guid TestTypeId, TestTypeSaveRequest Request)? LastUpdateRequest { get; private set; }
    public List<(Guid TestTypeId, TestTypeNoLongerInUseRequest Request)> AllNoLongerInUseRequests { get; } = [];
    public Guid? LastDeletedTestTypeId { get; private set; }
    public int? LastDeletedYearId { get; private set; }

    public Task<IReadOnlyList<TestTypeResponse>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items);

    public Task<TestTypeSaveResult> CreateAsync(TestTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;
        return Task.FromResult(SaveResult);
    }

    public Task<TestTypeSaveResult> UpdateAsync(Guid testTypeId, TestTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastUpdateRequest = (testTypeId, request);
        return Task.FromResult(SaveResult);
    }

    public Task<TestTypeSaveResult> SetNoLongerInUseAsync(Guid testTypeId, TestTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default)
    {
        AllNoLongerInUseRequests.Add((testTypeId, request));
        return Task.FromResult(SaveResult);
    }

    public Task<TestTypeDeleteResponse> DeleteAsync(Guid testTypeId, int yearId, CancellationToken cancellationToken = default)
    {
        LastDeletedTestTypeId = testTypeId;
        LastDeletedYearId = yearId;
        return Task.FromResult(DeleteResponse);
    }
}
