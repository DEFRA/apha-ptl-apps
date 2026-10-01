using PTL.Core.Viewer;

namespace PTL.Api.Tests.Participant;

internal sealed class FakeViewerRepository : IViewerRepository
{
    public List<ViewerEntity> Viewers { get; } = [];

    public Task<IReadOnlyList<ViewerEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ViewerEntity>>(Viewers);
}
