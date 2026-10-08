namespace PTL.Core.Viewer;

// tblViewer - legacy Viewers.ViewerInfo. Read-only master data; viewers are not managed here.
public sealed class ViewerEntity
{
    public Guid ViewerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid SsoId { get; set; }
    public Guid? SsoIdExt { get; set; }
}

public interface IViewerRepository
{
    // spgaViewers result set 1 - every viewer, ordered by name.
    Task<IReadOnlyList<ViewerEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the viewer row matching a CIDM contact id via <c>spgViewerBySsoId</c>.</summary>
    Task<ViewerEntity?> GetBySsoIdExtAsync(Guid ssoIdExt, CancellationToken cancellationToken = default);

    /// <summary>Reads the viewer row matching an email address via <c>spgViewerByEmail</c>, used only as a CIDM fallback.</summary>
    Task<ViewerEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<ViewerEntity> CreateAsync(ViewerEntity viewer, CancellationToken cancellationToken = default);
    Task<ViewerEntity?> UpdateAsync(ViewerEntity viewer, CancellationToken cancellationToken = default);

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
