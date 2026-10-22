using PTL.ApiClient;
using PTL.Contracts.Viewer;

namespace PTL.InternalWeb.Tests.TestSupport;

// Test double for IViewerApiClient so SystemAdministrationController tests don't need a real
// HTTP call to PTL.Api.
internal sealed class FakeViewerApiClient : IViewerApiClient
{
    public IReadOnlyList<ViewerResponse> Viewers { get; set; } = [];
    public ViewerSaveResult SaveResult { get; set; } = new(true, null, new Dictionary<string, string[]>());
    public ViewerDeleteResponse DeleteResult { get; set; } = new(true, null);
    public ViewerSaveRequest? LastCreateRequest { get; private set; }
    public List<(Guid ViewerId, ViewerSaveRequest Request)> UpdateCalls { get; } = [];
    public List<Guid> DeleteCalls { get; } = [];

    public Task<IReadOnlyList<ViewerResponse>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Viewers);

    public Task<ViewerSaveResult> CreateAsync(ViewerSaveRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;
        return Task.FromResult(SaveResult);
    }

    public Task<ViewerSaveResult> UpdateAsync(Guid viewerId, ViewerSaveRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((viewerId, request));
        return Task.FromResult(SaveResult);
    }

    public Task<ViewerDeleteResponse> DeleteAsync(Guid viewerId, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(viewerId);
        return Task.FromResult(DeleteResult);
    }
}
