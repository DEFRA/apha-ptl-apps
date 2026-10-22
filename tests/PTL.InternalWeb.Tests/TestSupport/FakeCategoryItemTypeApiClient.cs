using PTL.ApiClient;
using PTL.Contracts.CategoryItemType;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for ICategoryItemTypeApiClient so SchemeAdminController tests don't need a real
// HTTP call to PTL.Api.
internal sealed class FakeCategoryItemTypeApiClient : ICategoryItemTypeApiClient
{
    public IReadOnlyList<CategoryItemTypeResponse> Items { get; set; } = [];
    public CategoryItemTypeSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());
    public CategoryItemTypeDeleteResponse DeleteResponse { get; set; } = new(true, null);
    public CategoryItemTypeSaveRequest? LastCreateRequest { get; private set; }
    public (Guid CategoryItemTypeId, CategoryItemTypeSaveRequest Request)? LastUpdateRequest { get; private set; }
    public (Guid CategoryItemTypeId, CategoryItemTypeNoLongerInUseRequest Request)? LastNoLongerInUseRequest { get; private set; }
    public List<(Guid CategoryItemTypeId, CategoryItemTypeNoLongerInUseRequest Request)> AllNoLongerInUseRequests { get; } = [];
    public Guid? LastDeletedCategoryItemTypeId { get; private set; }

    // Set to simulate a downstream API failure (e.g. HttpRequestException) during
    // SetNoLongerInUseAsync, exercising SchemeAdminController.IsTransientApiFailure.
    public Exception? SetNoLongerInUseException { get; set; }

    public Task<IReadOnlyList<CategoryItemTypeResponse>> GetByYearAsync(int yearId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items);

    public Task<CategoryItemTypeSaveResult> CreateAsync(CategoryItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;
        return Task.FromResult(SaveResult);
    }

    public Task<CategoryItemTypeSaveResult> UpdateAsync(Guid categoryItemTypeId, CategoryItemTypeSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastUpdateRequest = (categoryItemTypeId, request);
        return Task.FromResult(SaveResult);
    }

    public Task<CategoryItemTypeSaveResult> SetNoLongerInUseAsync(Guid categoryItemTypeId, CategoryItemTypeNoLongerInUseRequest request, CancellationToken cancellationToken = default)
    {
        if (SetNoLongerInUseException is not null)
        {
            throw SetNoLongerInUseException;
        }

        LastNoLongerInUseRequest = (categoryItemTypeId, request);
        AllNoLongerInUseRequests.Add((categoryItemTypeId, request));
        return Task.FromResult(SaveResult);
    }

    public Task<CategoryItemTypeDeleteResponse> DeleteAsync(Guid categoryItemTypeId, CancellationToken cancellationToken = default)
    {
        LastDeletedCategoryItemTypeId = categoryItemTypeId;
        return Task.FromResult(DeleteResponse);
    }
}
