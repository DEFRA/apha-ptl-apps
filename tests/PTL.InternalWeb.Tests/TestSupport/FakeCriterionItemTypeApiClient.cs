using PTL.ApiClient;
using PTL.Contracts.CriterionItemType;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for ICriterionItemTypeApiClient so SchemeAdminController tests don't need a real
// HTTP call to PTL.Api. Mirrors FakeTestTypeApiClient - DeleteAsync takes yearId since the delete
// semantics are year-scoped.
internal sealed class FakeCriterionItemTypeApiClient : ICriterionItemTypeApiClient
{
    public IReadOnlyList<CriterionItemTypeResponse> Items { get; set; } = [];
    public CriterionItemTypeSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());
    public CriterionItemTypeDeleteResponse DeleteResponse { get; set; } = new(true, null);
    public CriterionItemTypeSaveRequest? LastCreateRequest { get; private set; }
    public (Guid CriterionItemTypeId, CriterionItemTypeSaveRequest Request)? LastUpdateRequest { get; private set; }
    public List<(Guid CriterionItemTypeId, CriterionItemTypeNoLongerInUseRequest Request)> AllNoLongerInUseRequests { get; } = [];
    public Guid? LastDeletedCriterionItemTypeId { get; private set; }
    public int? LastDeletedYearId { get; private set; }

    public Task<IReadOnlyList<CriterionItemTypeResponse>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items);

    public Task<CriterionItemTypeSaveResult> CreateAsync(CriterionItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;
        return Task.FromResult(SaveResult);
    }

    public Task<CriterionItemTypeSaveResult> UpdateAsync(Guid criterionItemTypeId, CriterionItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastUpdateRequest = (criterionItemTypeId, request);
        return Task.FromResult(SaveResult);
    }

    public Task<CriterionItemTypeSaveResult> SetNoLongerInUseAsync(Guid criterionItemTypeId, CriterionItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default)
    {
        AllNoLongerInUseRequests.Add((criterionItemTypeId, request));
        return Task.FromResult(SaveResult);
    }

    public Task<CriterionItemTypeDeleteResponse> DeleteAsync(Guid criterionItemTypeId, int yearId, CancellationToken cancellationToken = default)
    {
        LastDeletedCriterionItemTypeId = criterionItemTypeId;
        LastDeletedYearId = yearId;
        return Task.FromResult(DeleteResponse);
    }
}
