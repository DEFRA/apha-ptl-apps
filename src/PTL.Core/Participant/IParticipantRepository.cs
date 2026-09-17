namespace PTL.Core.Participant;

public interface IParticipantRepository
{
    Task<Participant?> GetByIdAsync(Guid participantId, CancellationToken cancellationToken = default);
    Task<Participant?> GetBySsoIdAsync(Guid ssoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParticipantSummaryEntity>> GetSummariesAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Participant> CreateAsync(Participant participant, CancellationToken cancellationToken = default);
    Task<Participant?> UpdateAsync(Participant participant, CancellationToken cancellationToken = default);
}
