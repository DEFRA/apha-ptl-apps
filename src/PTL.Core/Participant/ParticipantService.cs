using Microsoft.Extensions.Logging;

namespace PTL.Core.Participant;

public sealed class ParticipantService(
    IParticipantRepository participantRepository,
    IPendingParticipantUpdateRepository pendingParticipantUpdateRepository,
    ILogger<ParticipantService> logger) : IParticipantService
{
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

    public async Task<ParticipantSearchResult> SearchParticipantsAsync(Guid customerId, string? searchTerm, bool includeInactive, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var all = await participantRepository.GetSummariesAsync(customerId, includeInactive, cancellationToken);
        var filtered = string.IsNullOrWhiteSpace(searchTerm)
            ? all
            : all.Where(p =>
                p.LabName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.LabCode.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.ContactName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var totalCount = filtered.Count;
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
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
    public async Task<bool> ApprovePendingParticipantUpdateAsync(Guid participantId, PendingParticipantUpdate? editedFields = null, CancellationToken cancellationToken = default)
    {
        var existing = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        var pending = await pendingParticipantUpdateRepository.GetByParticipantIdAsync(participantId, cancellationToken);
        if (existing is null || pending is null)
        {
            return false;
        }

        ApplyPendingFields(existing, editedFields ?? pending);
        Validate(existing);
        await participantRepository.UpdateAsync(existing, cancellationToken);
        return await pendingParticipantUpdateRepository.MarkDecidedAsync(participantId, pending.PendingParticipantUpdateId, cancellationToken);
    }

    // Declined changes do not update the live record - only the pending row is soft-deleted.
    public async Task<bool> DeclinePendingParticipantUpdateAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var pending = await pendingParticipantUpdateRepository.GetByParticipantIdAsync(participantId, cancellationToken);
        return pending is not null && await pendingParticipantUpdateRepository.MarkDecidedAsync(participantId, pending.PendingParticipantUpdateId, cancellationToken);
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
