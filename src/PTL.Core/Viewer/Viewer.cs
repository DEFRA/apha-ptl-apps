namespace PTL.Core.Viewer;

// tblViewer - legacy Viewers.ViewerInfo. Originally read-only master data for CIDM external-user
// resolution only; now also backs the Manage Viewers admin screen (add/edit/remove + the stubbed
// Generate Login action).
public sealed class ViewerEntity
{
    public Guid ViewerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid SsoId { get; set; }
    public Guid? SsoIdExt { get; set; }

    // Only populated by GetAllWithAssignmentsAsync (the admin grid's read) - GetAllAsync's plain
    // single-result-set read leaves these at their empty default, matching its existing callers
    // (e.g. ParticipantService's "available viewers to assign" list) which never needed them.
    public IReadOnlyList<ViewerSchemeEntity> Schemes { get; set; } = [];
    public IReadOnlyList<ViewerParticipantLinkEntity> Participants { get; set; } = [];
}

// tlnkViewerScheme joined to tblScheme - one row per scheme this viewer can currently see.
public sealed class ViewerSchemeEntity
{
    public Guid ViewerId { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

// tlnkViewerParticipant joined to tblParticipant - one row per participant this viewer can
// currently see.
public sealed class ViewerParticipantLinkEntity
{
    public Guid ViewerId { get; set; }
    public string LabCode { get; set; } = string.Empty;
    public string LabName { get; set; } = string.Empty;
}

public interface IViewerRepository
{
    // spgaViewers result set 1 - every viewer, ordered by name. Schemes/Participants are left
    // empty; use GetAllWithAssignmentsAsync when the grid needs them.
    Task<IReadOnlyList<ViewerEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    // spgaViewers all 3 result sets - every viewer with its assigned schemes/participants
    // attached, for the Manage Viewers admin grid's remove-confirmation listing.
    Task<IReadOnlyList<ViewerEntity>> GetAllWithAssignmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the viewer row matching a CIDM contact id via <c>spgViewerBySsoId</c>.</summary>
    Task<ViewerEntity?> GetBySsoIdExtAsync(Guid ssoIdExt, CancellationToken cancellationToken = default);

    /// <summary>Reads the viewer row matching an email address via <c>spgViewerByEmail</c>, used only as a CIDM fallback.</summary>
    Task<ViewerEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<ViewerEntity> CreateAsync(ViewerEntity viewer, CancellationToken cancellationToken = default);
    Task<ViewerEntity?> UpdateAsync(ViewerEntity viewer, CancellationToken cancellationToken = default);

    // spdViewer - unconditional delete, no allocation block (matches legacy; see
    // ViewerDeleteResponse remarks). Returns false if the viewer no longer exists.
    Task<bool> DeleteAsync(Guid viewerId, CancellationToken cancellationToken = default);
}

