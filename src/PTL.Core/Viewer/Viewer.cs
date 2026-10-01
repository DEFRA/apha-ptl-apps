namespace PTL.Core.Viewer;

// tblViewer - legacy Viewers.ViewerInfo. Read-only master data; viewers are not managed here.
public sealed class ViewerEntity
{
    public Guid ViewerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid SsoId { get; set; }
}

public interface IViewerRepository
{
    // spgaViewers result set 1 - every viewer, ordered by name.
    Task<IReadOnlyList<ViewerEntity>> GetAllAsync(CancellationToken cancellationToken = default);
}
