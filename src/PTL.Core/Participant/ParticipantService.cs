using Microsoft.Extensions.Logging;

namespace PTL.Core.Participant;

public sealed class ParticipantService(IParticipantRepository participantRepository, ILogger<ParticipantService> logger) : IParticipantService
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

        var updated = await participantRepository.UpdateAsync(updatedFields, cancellationToken);
        LogUpdatedParticipantMessage(logger, participantId, null);
        return updated;
    }
}
