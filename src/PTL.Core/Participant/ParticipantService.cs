using Microsoft.Extensions.Logging;

namespace PTL.Core.Participant;

public sealed class ParticipantService(IParticipantRepository participantRepository, ILogger<ParticipantService> logger) : IParticipantService
{
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
        logger.LogInformation("Created participant {ParticipantId} ({LabCode})", created.ParticipantId, created.LabCode);
        return created;
    }

    public async Task<Participant?> UpdateParticipantAsync(Guid participantId, Participant updatedFields, CancellationToken cancellationToken = default)
    {
        var existing = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (existing is null)
        {
            logger.LogWarning("Update requested for unknown participant {ParticipantId}", participantId);
            return null;
        }

        updatedFields.ParticipantId = existing.ParticipantId;
        updatedFields.SsoId = existing.SsoId;
        updatedFields.CustomerId = existing.CustomerId;
        updatedFields.InactiveDate ??= updatedFields.IsActive ? null : DateTime.UtcNow;

        var updated = await participantRepository.UpdateAsync(updatedFields, cancellationToken);
        logger.LogInformation("Updated participant {ParticipantId}", participantId);
        return updated;
    }

    public async Task<Participant?> DeactivateParticipantAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var existing = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (existing is null)
        {
            logger.LogWarning("Deactivate requested for unknown participant {ParticipantId}", participantId);
            return null;
        }

        existing.IsActive = false;
        existing.InactiveDate ??= DateTime.UtcNow;

        var updated = await participantRepository.UpdateAsync(existing, cancellationToken);
        logger.LogInformation("Deactivated participant {ParticipantId}", participantId);
        return updated;
    }

    public async Task<Participant?> ReactivateParticipantAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        var existing = await participantRepository.GetByIdAsync(participantId, cancellationToken);
        if (existing is null)
        {
            logger.LogWarning("Reactivate requested for unknown participant {ParticipantId}", participantId);
            return null;
        }

        existing.IsActive = true;
        existing.InactiveDate = null;
        existing.InactiveError = false;
        existing.InactiveErrorDate = null;

        var updated = await participantRepository.UpdateAsync(existing, cancellationToken);
        logger.LogInformation("Reactivated participant {ParticipantId}", participantId);
        return updated;
    }
}
