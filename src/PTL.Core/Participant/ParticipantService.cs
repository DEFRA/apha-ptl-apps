using Microsoft.Extensions.Logging;
using PTL.Core.Viewer;

namespace PTL.Core.Participant;

public sealed class ParticipantService(
    IParticipantRepository participantRepository,
    IPendingParticipantUpdateRepository pendingParticipantUpdateRepository,
    IParticipantUpdateNotifier participantUpdateNotifier,
    IParticipantViewerRepository participantViewerRepository,
    IViewerRepository viewerRepository,
    ILogger<ParticipantService> logger) : IParticipantService
{
    // spiUpdatedParticipantDetailsEmail takes @Email varchar(100); tblUpdatedParticipantDetailsEmail.fldLabCode is varchar(8)
    // and fldParticipantName varchar(50).
    private const int EmailAddressMaxLength = 100;
    private const int LabCodeMaxLength = 8;
    private const int ParticipantNameMaxLength = 50;

    // PendingParticipantUpdateDetails.aspx's comments regex.
    private const int CommentsMaxLength = 2000;

    private static readonly Action<ILogger, Guid, string, Exception?> LogCreatedParticipantMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Information,
            new EventId(1, nameof(LogCreatedParticipantMessage)),
            "Created participant {ParticipantId} ({LabCode})");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdateRequestedForUnknownParticipantMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(2, nameof(LogUpdateRequestedForUnknownParticipantMessage)),
            "Update requested for unknown participant {ParticipantId}");

    private static readonly Action<ILogger, Guid, Exception?> LogUpdatedParticipantMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(3, nameof(LogUpdatedParticipantMessage)),
            "Updated participant {ParticipantId}");

    private static readonly Action<ILogger, Guid, Exception?> LogDeactivateRequestedForUnknownParticipantMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(4, nameof(LogDeactivateRequestedForUnknownParticipantMessage)),
            "Deactivate requested for unknown participant {ParticipantId}");

    private static readonly Action<ILogger, Guid, Exception?> LogDeactivatedParticipantMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(5, nameof(LogDeactivatedParticipantMessage)),
            "Deactivated participant {ParticipantId}");

    private static readonly Action<ILogger, Guid, Exception?> LogReactivateRequestedForUnknownParticipantMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(6, nameof(LogReactivateRequestedForUnknownParticipantMessage)),
            "Reactivate requested for unknown participant {ParticipantId}");

    private static readonly Action<ILogger, Guid, Exception?> LogReactivatedParticipantMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(7, nameof(LogReactivatedParticipantMessage)),
            "Reactivated participant {ParticipantId}");

    public Task<Participant?> GetParticipantAsync(Guid participantId, CancellationToken cancellationToken = default) =>
        participantRepository.GetByIdAsync(participantId, cancellationToken);

    public Task<IReadOnlyList<ParticipantSummaryEntity>> GetParticipantsAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default) =>
        participantRepository.GetSummariesAsync(customerId, includeInactive, cancellationToken);

    public async Task<ParticipantSearchResult> SearchParticipantsAsync(Guid customerId, bool includeInactive, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var all = await participantRepository.GetSummariesAsync(customerId, includeInactive, cancellationToken);

        var totalCount = all.Count;
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new ParticipantSearchResult(items, totalCount);
    }

    public async Task<Participant> CreateParticipantAsync(Participant participant, CancellationToken cancellationToken = default)
    {
        Validate(participant);

        if (participant.ParticipantId == Guid.Empty)
        {
            participant.ParticipantId = Guid.NewGuid();
        }

        if (participant.SsoId == Guid.Empty)
        {
            participant.SsoId = Guid.NewGuid();
        }

        participant.InactiveDate ??= participant.IsActive ? null : DateTime.UtcNow;
        var created = await participantRepository.CreateAsync(participant, cancellationToken);
        LogCreatedParticipantMessage(logger, created.ParticipantId, created.LabCode, null);
        return created;
    }

    public async Task<Participant?> UpdateParticipantAsync(Guid participantId, Participant updatedFields, CancellationToken cancellationToken = default)
    {
        var existing = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (existing is null)
        {
            LogUpdateRequestedForUnknownParticipantMessage(logger, participantId, null);
            return null;
        }

        updatedFields.ParticipantId = existing.ParticipantId;
        updatedFields.SsoId = existing.SsoId;
        updatedFields.CustomerId = existing.CustomerId;
        updatedFields.InactiveDate ??= updatedFields.IsActive ? null : DateTime.UtcNow;

        Validate(updatedFields);

        var updated = await participantRepository.UpdateAsync(updatedFields, cancellationToken);
        LogUpdatedParticipantMessage(logger, participantId, null);
        return updated;
    }

    public async Task<Participant?> DeactivateParticipantAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var existing = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (existing is null)
        {
            LogDeactivateRequestedForUnknownParticipantMessage(logger, participantId, null);
            return null;
        }

        existing.IsActive = false;
        existing.InactiveDate ??= DateTime.UtcNow;

        Validate(existing);

        var updated = await participantRepository.UpdateAsync(existing, cancellationToken);
        LogDeactivatedParticipantMessage(logger, participantId, null);
        return updated;
    }

    public async Task<Participant?> ReactivateParticipantAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var existing = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (existing is null)
        {
            LogReactivateRequestedForUnknownParticipantMessage(logger, participantId, null);
            return null;
        }

        existing.IsActive = true;
        existing.InactiveDate = null;

        Validate(existing);

        var updated = await participantRepository.UpdateAsync(existing, cancellationToken);
        LogReactivatedParticipantMessage(logger, participantId, null);
        return updated;
    }

    private static void Validate(Participant participant)
    {
        var result = ParticipantValidator.Validate(participant);
        if (!result.IsValid)
        {
            throw new ParticipantValidationException(result.Errors);
        }
    }

    public Task<IReadOnlyList<PendingParticipantUpdateSummaryEntity>> GetPendingParticipantUpdatesAsync(CancellationToken cancellationToken = default) =>
        pendingParticipantUpdateRepository.GetSummariesAsync(cancellationToken);

    public async Task<(Participant Current, PendingParticipantUpdate Pending)?> GetPendingParticipantUpdateAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var current = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        var pending = await pendingParticipantUpdateRepository.GetByParticipantIdAsync(participantId, cancellationToken);
        return current is null || pending is null ? null : (current, pending);
    }

    // Only approved changes update the live participant record - matches
    // PendingParticipantUpdateDetails.aspx's ButtonApprove_Click, including its mParticipant.IsValid check.
    public async Task<bool> ApprovePendingParticipantUpdateAsync(Guid participantId, PendingParticipantUpdate? editedFields = null, string comments = "", CancellationToken cancellationToken = default)
    {
        comments ??= string.Empty;
        ValidateComments(comments);

        var existing = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        var pending = await pendingParticipantUpdateRepository.GetByParticipantIdAsync(participantId, cancellationToken);
        if (existing is null || pending is null)
        {
            return false;
        }

        ApplyPendingFields(existing, editedFields ?? pending);
        Validate(existing);
        await participantRepository.UpdateAsync(existing, cancellationToken);
        var decided = await pendingParticipantUpdateRepository.MarkDecidedAsync(participantId, pending.PendingParticipantUpdateId, cancellationToken);
        if (decided)
        {
            // Legacy read the participant back after saving it, so approved email changes are notified to the new addresses.
            await NotifyAsync(existing, pending.PendingParticipantUpdateId, comments, isApproved: true, cancellationToken);
        }

        return decided;
    }

    // Declined changes do not update the live record - only the pending row is soft-deleted.
    public async Task<bool> DeclinePendingParticipantUpdateAsync(Guid participantId, string comments = "", CancellationToken cancellationToken = default)
    {
        comments ??= string.Empty;
        ValidateComments(comments);

        var pending = await pendingParticipantUpdateRepository.GetByParticipantIdAsync(participantId, cancellationToken);
        if (pending is null)
        {
            return false;
        }

        var decided = await pendingParticipantUpdateRepository.MarkDecidedAsync(participantId, pending.PendingParticipantUpdateId, cancellationToken);
        if (decided && await participantRepository.GetByIdAsync(participantId, cancellationToken) is { } participant)
        {
            await NotifyAsync(participant, pending.PendingParticipantUpdateId, comments, isApproved: false, cancellationToken);
        }

        return decided;
    }

    private static void ValidateComments(string comments)
    {
        if (comments.Length > CommentsMaxLength)
        {
            throw new ParticipantValidationException([new ParticipantValidationError("Comments", "The comments field allows maximum 2000 characters")]);
        }
    }

    // Legacy EmailUpdateNotifications: one row for the participant's primary email and one for the secondary when it is
    // set, which are then sent at once (a failed send is not retried). Blank addresses are skipped rather than queued.
    private async Task NotifyAsync(Participant participant, Guid pendingParticipantUpdateId, string comments, bool isApproved, CancellationToken cancellationToken)
    {
        var labCode = Truncate(participant.LabCode, LabCodeMaxLength);
        var participantName = Truncate($"{participant.LabCode}: {participant.LabName}", ParticipantNameMaxLength);

        var queued = false;
        foreach (var address in new[] { participant.Email, participant.Email2 }.Where(a => !string.IsNullOrWhiteSpace(a)))
        {
            await pendingParticipantUpdateRepository.QueueNotificationAsync(
                new ParticipantUpdateNotification(Truncate(address, EmailAddressMaxLength), labCode, pendingParticipantUpdateId, participantName, comments, isApproved),
                cancellationToken);
            queued = true;
        }

        if (queued)
        {
            await participantUpdateNotifier.SendForUpdateAsync(pendingParticipantUpdateId, CancellationToken.None);
        }
    }

    private static string Truncate(string value, int length) => value.Length > length ? value[..length] : value;

    public async Task<ParticipantViewerAssignment?> GetParticipantViewersAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (participant is null)
        {
            return null;
        }

        var allViewers = await viewerRepository.GetAllAsync(cancellationToken);
        var assigned = await participantViewerRepository.GetByParticipantIdAsync(participantId, cancellationToken);
        var assignedViewerIds = assigned.Select(a => a.ViewerId).ToHashSet();

        // Legacy PopulateViewersListBoxes: Available = every viewer not already in the collection.
        var available = allViewers.Where(v => !assignedViewerIds.Contains(v.ViewerId)).ToList();

        return new ParticipantViewerAssignment(participant.CustomerId, participant.LabCode, participant.LabName, participant.IsActive, available, assigned);
    }

    public async Task<bool> UpdateParticipantViewersAsync(Guid participantId, IReadOnlyList<Guid> targetViewerIds, CancellationToken cancellationToken = default)
    {
        var participant = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (participant is null)
        {
            return false;
        }

        var requestedViewerIds = (targetViewerIds ?? []).Distinct().ToArray();
        var current = await participantViewerRepository.GetByParticipantIdAsync(participantId, cancellationToken);
        var targetIds = requestedViewerIds.ToHashSet();

        foreach (var removed in current.Where(c => !targetIds.Contains(c.ViewerId)))
        {
            await participantViewerRepository.RemoveAsync(removed.ViewerParticipantId, cancellationToken);
        }

        var currentViewerIds = current.Select(c => c.ViewerId).ToHashSet();
        foreach (var addedViewerId in targetIds.Where(id => !currentViewerIds.Contains(id)))
        {
            await participantViewerRepository.AddAsync(Guid.NewGuid(), addedViewerId, participantId, cancellationToken);
        }

        return true;
    }

    private static void ApplyPendingFields(Participant participant, PendingParticipantUpdate pending)
    {
        participant.ContactName = pending.ContactName;
        participant.Organisation = pending.Organisation;
        participant.Address1 = pending.Address1;
        participant.Address2 = pending.Address2;
        participant.Address3 = pending.Address3;
        participant.Address4 = pending.Address4;
        participant.Address5 = pending.Address5;
        participant.CountryId = pending.CountryId;
        participant.Telephone = pending.Telephone;
        participant.Fax = pending.Fax;
        participant.Email = pending.Email;
        participant.Email2 = pending.Email2;
    }
}
