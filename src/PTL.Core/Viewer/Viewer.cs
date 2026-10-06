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

    // spgaViewers result set 2 - the tlnkViewerScheme links for one scheme.
    Task<IReadOnlyList<SchemeViewerEntity>> GetSchemeViewersAsync(Guid schemeId, CancellationToken cancellationToken = default);

    Task AddSchemeViewerAsync(Guid viewerSchemeId, Guid viewerId, Guid schemeId, CancellationToken cancellationToken = default);

    Task RemoveSchemeViewerAsync(Guid viewerSchemeId, CancellationToken cancellationToken = default);
}

// One tlnkViewerScheme row. Legacy generates fldViewerSchemeId client-side when a viewer is added
// and never updates a link - it is inserted or deleted.
public sealed class SchemeViewerEntity
{
    public Guid ViewerSchemeId { get; set; }
    public Guid ViewerId { get; set; }
    public Guid SchemeId { get; set; }
}
