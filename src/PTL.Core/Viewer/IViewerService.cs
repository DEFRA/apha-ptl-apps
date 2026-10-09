namespace PTL.Core.Viewer;

public interface IViewerService
{
    Task<IReadOnlyList<ViewerEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ViewerEntity> CreateAsync(string name, string email, CancellationToken cancellationToken = default);

    Task<ViewerEntity?> UpdateAsync(Guid viewerId, string name, string email, CancellationToken cancellationToken = default);

    Task<(bool Success, string? Message)> DeleteAsync(Guid viewerId, CancellationToken cancellationToken = default);

    Task<(bool Success, string? Message)> GenerateLoginAsync(Guid viewerId, CancellationToken cancellationToken = default);
}
