using PTL.Core.Audit;

namespace PTL.Core.Viewer;

public sealed class ViewerService(IViewerRepository repository, IActingUserAccessor actingUser) : IViewerService
{
    private const string EntityType = "Viewer";

    private AuditEntry Audit(string action, Guid id, string? details) =>
        new(actingUser.ActingUserId, action, EntityType, id.ToString(), details);

    public Task<IReadOnlyList<ViewerEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        repository.GetAllWithAssignmentsAsync(cancellationToken);

    public async Task<ViewerEntity> CreateAsync(string name, string email, CancellationToken cancellationToken = default)
    {
        Validate(name, email);

        var newViewer = new ViewerEntity
        {
            ViewerId = Guid.NewGuid(),
            Name = name.Trim(),
            Email = email.Trim(),
            SsoId = Guid.Empty,
            SsoIdExt = null
        };

        await repository.CreateAsync(newViewer, Audit("CreateViewer", newViewer.ViewerId, $"Name: {newViewer.Name}"), cancellationToken);
        return newViewer;
    }

    public async Task<ViewerEntity?> UpdateAsync(Guid viewerId, string name, string email, CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(viewerId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        Validate(name, email);

        existing.Name = name.Trim();
        existing.Email = email.Trim();

        await repository.UpdateAsync(existing, Audit("UpdateViewer", existing.ViewerId, $"Name: {existing.Name}"), cancellationToken);
        return existing;
    }

    public async Task<(bool Success, string? Message)> DeleteAsync(Guid viewerId, CancellationToken cancellationToken = default)
    {
        // Must use GetAllWithAssignmentsAsync (not FindAsync/plain GetAllAsync) - Schemes/
        // Participants are only populated by the richer read, and tlnkViewerScheme/
        // tlnkViewerParticipant both have an FK to tblViewer that blocks the delete outright if
        // either is non-empty. Matches legacy's intended (but dead/commented-out) block in
        // ManageViewers.aspx.vb, and the same pattern CountryService.DeleteAsync already uses.
        var existing = (await repository.GetAllWithAssignmentsAsync(cancellationToken)).FirstOrDefault(v => v.ViewerId == viewerId);
        if (existing is null)
        {
            return (false, "Viewer was not found.");
        }

        if (existing.Schemes.Count > 0 || existing.Participants.Count > 0)
        {
            return (false, "This viewer is assigned to one or more schemes or participants. Remove those assignments before removing the viewer.");
        }

        var deleted = await repository.DeleteAsync(viewerId, Audit("DeleteViewer", viewerId, $"Name: {existing.Name}"), cancellationToken);
        return deleted ? (true, null) : (false, "Viewer was not found.");
    }

    private async Task<ViewerEntity?> FindAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        var all = await repository.GetAllAsync(cancellationToken);
        return all.FirstOrDefault(v => v.ViewerId == viewerId);
    }

    private static void Validate(string name, string email)
    {
        var result = ViewerValidator.Validate(name, email);
        if (!result.IsValid)
        {
            throw new ViewerValidationException(result.Errors);
        }
    }
}
