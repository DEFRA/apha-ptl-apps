using PTL.Core.Viewer;

namespace PTL.Api.Tests.Participant;

internal sealed class FakeViewerRepository : IViewerRepository
{
    public List<ViewerEntity> Viewers { get; } = [];

    public Task<IReadOnlyList<ViewerEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ViewerEntity>>(Viewers);

    public Task<ViewerEntity?> GetBySsoIdExtAsync(Guid ssoIdExt, CancellationToken cancellationToken = default) =>
        Task.FromResult(Viewers.FirstOrDefault(v => v.SsoIdExt == ssoIdExt));

    public Task<ViewerEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Viewers.FirstOrDefault(v => string.Equals(v.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<ViewerEntity> CreateAsync(ViewerEntity viewer, CancellationToken cancellationToken = default)
    {
        viewer.ViewerId = viewer.ViewerId == Guid.Empty ? Guid.NewGuid() : viewer.ViewerId;
        Viewers.Add(viewer);
        return Task.FromResult(viewer);
    }

    public Task<ViewerEntity?> UpdateAsync(ViewerEntity viewer, CancellationToken cancellationToken = default)
    {
        var index = Viewers.FindIndex(v => v.ViewerId == viewer.ViewerId);
        if (index < 0)
        {
            return Task.FromResult<ViewerEntity?>(null);
        }

        Viewers[index] = viewer;
        return Task.FromResult<ViewerEntity?>(viewer);
    }
}
