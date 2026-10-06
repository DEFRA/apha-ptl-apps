using PTL.Core.Viewer;

namespace PTL.Api.Tests.Participant;

internal sealed class FakeViewerRepository : IViewerRepository
{
    public List<ViewerEntity> Viewers { get; } = [];

    public List<SchemeViewerEntity> SchemeViewers { get; } = [];

    public Task<IReadOnlyList<ViewerEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ViewerEntity>>(Viewers);

    public Task<IReadOnlyList<SchemeViewerEntity>> GetSchemeViewersAsync(Guid schemeId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SchemeViewerEntity>>(SchemeViewers.Where(v => v.SchemeId == schemeId).ToList());

    public Task AddSchemeViewerAsync(Guid viewerSchemeId, Guid viewerId, Guid schemeId, CancellationToken cancellationToken = default)
    {
        SchemeViewers.Add(new SchemeViewerEntity { ViewerSchemeId = viewerSchemeId, ViewerId = viewerId, SchemeId = schemeId });
        return Task.CompletedTask;
    }

    public Task RemoveSchemeViewerAsync(Guid viewerSchemeId, CancellationToken cancellationToken = default)
    {
        SchemeViewers.RemoveAll(v => v.ViewerSchemeId == viewerSchemeId);
        return Task.CompletedTask;
    }
}
