namespace PTL.Core.Participant;

public interface IParticipantService
{
    Task<Participant?> GetParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParticipantSummaryEntity>> GetParticipantsAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ParticipantSearchResult> SearchParticipantsAsync(Guid customerId, string? searchTerm, bool includeInactive, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Participant> CreateParticipantAsync(Participant participant, CancellationToken cancellationToken = default);
    Task<Participant?> UpdateParticipantAsync(Guid participantId, Participant updatedFields, CancellationToken cancellationToken = default);
}
